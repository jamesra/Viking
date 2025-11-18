###########################################
Identity Services Certificate Instructions
###########################################

This document provides instructions for generating and installing certificates for Identity Services.

These instructions assume you are using LetsEncrypt via CertBot on a WSL Ubuntu install.

###########################################
CertBot Installation
###########################################

Based upon the CertBot documentation https://certbot.eff.org/instructions?ws=webproduct&os=pip&tab=wildcard

To install CertBot, run the following commands in your WSL Ubuntu terminal:

#Install dependencies and Python3
sudo apt update
sudo apt install python3 python3-dev python3-venv libaugeas-dev gcc

#Create a virtual environment for certbot
sudo python3 -m venv /opt/certbot/
sudo /opt/certbot/bin/pip install --upgrade pip

#Install CertBot package via pip
sudo /opt/certbot/bin/pip install certbot

#Make certbot command globally available
sudo ln -s /opt/certbot/bin/certbot /usr/bin/certbot

#We use cloudflare, so we need to install the cloudflare plugin:
sudo /opt/certbot/bin/pip install certbot-dns-cloudflare

#Create an .ini file to store your Cloudflare API token (Obtained via the Cloudflare dashboard)
sudo nano /opt/certbot/cloudflare.ini

#Add the following line to the file, replacing YOUR_CLOUDFLARE_API_TOKEN with your actual token:
dns_cloudflare_api_token = YOUR_CLOUDFLARE_API_TOKEN
#Save and exit the file (Ctrl+X, Y, Enter)

#Secure the file so only root can read it
sudo chmod 600 /opt/certbot/cloudflare.ini

#Invoke certbot to generate a wildcard certificate for your domain (replace example.com with your actual domain):
sudo certbot certonly --dns-cloudflare --dns-cloudflare-credentials cloudflare.ini -d *.yourdomain

#Follow the prompts to complete the certificate generation process.
#Your certificates will be stored in /etc/letsencrypt/live/yourdomain/

###########################################

#############################################
Certificate Installation to docker containers
#############################################

_____________________________
IIS use - Conversion to PFX
-----------------------------

# Convert the generated certificate to PFX format for IIS use:
sudo openssl pkcs12 -export -out /etc/letsencrypt/live/yourdomain/identityservices.pfx -inkey /etc/letsencrypt/live/yourdomain/privkey.pem -in /etc/letsencrypt/live/yourdomain/fullchain.pem -password pass:YourPassword
# Replace 'YourPassword' with a strong password if a password is used

#Copy the PFX file to your Windows filesystem for IIS import.

-----------------------------------
Docker use - Copy to Docker Volumes
-----------------------------------

#Copy the certificate files to the Docker host system:

sudo cp /etc/letsencrypt/live/yourdomain/fullchain.pem /mnt/c/path/to/your/docker/volumes/certs/fullchain.pem
sudo cp /etc/letsencrypt/live/yourdomain/privkey.pem /mnt/c/path/to/your/docker/volumes/certs/privkey.pem

#Ensure the Docker containers are configured to use these certificate files from the specified paths.  This can be done via the .yml file in docker compose and a secrets file.

::
    version: '3.8'
    services:
      identity-standalone:
        build:
          context: ..
          dockerfile: IdentityServer/IdentityServerStandalone/Dockerfile
        ports:
          - "6000:6000"
          - "6001:6001"
        image: identity-standalone
        secrets:
          - ssl_cert
        environment:
          - SSL_CERT_PATH=/run/secrets/ssl_cert
        networks:
          - identity-network

      identity-webapi:
        build:
          context: ..
          dockerfile: IdentityServer/Viking.Identity.Server.WebApi/Dockerfile
        ports:
          - "5000:5000"
          - "5001:5001"
        image: identity-webapi
        secrets:
          - ssl_cert
        environment:
          - SSL_CERT_PATH=/run/secrets/ssl_cert
        networks:
          - identity-network

    secrets:
      ssl_cert:
        file: C:\Users\u0490822\certs\fullchain.pem

    networks:
      identity-network:
        driver: bridge


        
#If the containers are already running old certificates take them down first:
docker-compose -f IdentityServer/docker-compose.yml down

#To launch the containers with the updated certificates, use:
docker-compose -f IdentityServer/docker-compose.yml up -d

