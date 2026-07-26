# Microsoft Store Submission Guide for Viking

This guide will help you submit the Viking app to the Microsoft Store to obtain a trusted code signing certificate for your MSIX packages.

## 🎯 Benefits of Microsoft Store Approach

- **Cost:** Only $19 one-time registration fee
- **Trust:** Fully trusted by Windows SmartScreen
- **Auto-updates:** Seamless automatic updates
- **Distribution:** Easy deployment to users
- **Certificate:** Microsoft provides trusted code signing certificate

## 📋 Prerequisites

1. **Microsoft Account** (free)
2. **$19 Developer Registration Fee** (one-time)
3. **Windows 10/11** for testing
4. **Viking MSIX Package** (created by our scripts)

## 🚀 Step-by-Step Process

### Step 1: Create Microsoft Developer Account

1. **Go to:** https://partner.microsoft.com/dashboard
2. **Sign in** with your Microsoft account
3. **Click "Get started"** or "Join now"
4. **Pay the $19 registration fee**
5. **Complete your developer profile:**
   - Company name: Marc Lab
   - Contact information
   - Payment method

### Step 2: Create App Submission

1. **In Partner Center Dashboard:**
   - Click **"Create new app"**
   - Select **"Desktop application"**

2. **Fill in App Information:**
   ```
   App name: Viking
   App type: Desktop application
   Category: Science & Technology
   Subcategory: Research & Reference
   ```

3. **App Description:**
   ```
   Viking is a powerful application for connectome analysis and neuroscience research. 
   It provides advanced tools for brain mapping, neural network visualization, and 
   scientific data analysis.
   ```

### Step 3: Prepare Store Package

Run our store package creation script:

```powershell
.\Scripts\CreateStorePackage.ps1
```

This creates a store-ready MSIX package in `Publish\StorePackage\Viking-1.2.0.0.msix`

### Step 4: Upload Package

1. **In Partner Center:**
   - Go to **"Packages"** section
   - Click **"Upload packages"**
   - Upload: `Publish\StorePackage\Viking-1.2.0.0.msix`

2. **Package Information:**
   - **Version:** 1.2.0.0
   - **Architecture:** x64
   - **Minimum OS:** Windows 10, version 1809

### Step 5: App Store Listing

1. **App Name:** Viking
2. **Description:**
   ```
   Viking is a comprehensive neuroscience research tool designed for connectome analysis. 
   Features include:
   
   • Advanced brain mapping and visualization
   • Neural network analysis tools
   • Scientific data processing capabilities
   • High-performance graphics rendering
   • Export and sharing functionality
   
   Perfect for researchers, neuroscientists, and anyone working with brain connectivity data.
   ```

3. **Keywords:** neuroscience, brain, connectome, research, analysis, mapping

4. **Category:** Science & Technology > Research & Reference

### Step 6: Pricing & Availability

1. **Pricing:** Free (or set your desired price)
2. **Availability:** 
   - **Markets:** Select relevant countries
   - **Visibility:** Public (or Private for testing)
3. **Age Rating:** 3+ (General)

### Step 7: Submit for Review

1. **Review all sections** (green checkmarks)
2. **Click "Submit to the Store"**
3. **Wait for review** (typically 1-3 business days)

## 🔐 Getting Your Code Signing Certificate

### After Store Approval:

1. **Download the signed package** from Partner Center
2. **Extract the certificate** from the signed package
3. **Use for future MSIX packages**

### Certificate Location:
- **Signed package:** Downloaded from Partner Center
- **Certificate:** Embedded in the signed MSIX
- **Usage:** Can be extracted and reused

## 📱 Alternative: Private Store Distribution

If you don't want public listing:

1. **Set visibility to "Private"**
2. **Submit for review** (still required)
3. **Get certificate** without public exposure
4. **Distribute privately** to your users

## 🛠️ Using the Certificate

Once you have the Microsoft-signed package:

```powershell
# Extract certificate from signed package
# Use for signing future packages
signtool sign /f "microsoft-certificate.pfx" /p "password" "Viking-1.2.0.0.msix"
```

## 📋 Store Requirements Checklist

- [ ] Developer account created ($19 paid)
- [ ] App information completed
- [ ] Store package created
- [ ] Package uploaded
- [ ] App listing filled out
- [ ] Privacy policy (if collecting data)
- [ ] Age rating completed
- [ ] Pricing set
- [ ] Markets selected
- [ ] Submitted for review

## 🎯 Next Steps After Approval

1. **Download signed package**
2. **Extract certificate**
3. **Update deployment scripts**
4. **Deploy to your web server**
5. **Set up auto-updates**

## 💡 Tips for Success

1. **Be thorough** with app description
2. **Use clear screenshots** (if available)
3. **Test thoroughly** before submission
4. **Respond quickly** to any review feedback
5. **Keep app updated** regularly

## 🔗 Useful Links

- **Partner Center:** https://partner.microsoft.com/dashboard
- **Store Policies:** https://docs.microsoft.com/en-us/windows/uwp/publish/store-policies
- **App Submission:** https://docs.microsoft.com/en-us/windows/uwp/publish/app-submissions
- **MSIX Documentation:** https://docs.microsoft.com/en-us/windows/msix/

## 🆘 Support

If you encounter issues:
- **Partner Center Support:** Available in dashboard
- **Microsoft Documentation:** Comprehensive guides available
- **Community Forums:** Active developer community

---

**Note:** This process gives you a trusted certificate for $19, which is significantly cheaper than commercial code signing certificates ($200-500/year).

















































