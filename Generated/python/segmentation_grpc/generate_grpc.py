"""
Generate gRPC Code

This script generates the Python code for the gRPC service from the proto file.
It uses the protoc compiler to generate the code.
"""

import os
import sys
import subprocess

def generate_grpc_code(force: bool):
    """Generate the gRPC code from the proto file.
    :param force: If True, force generation even if the proto file hasn't been modified since the last generation.  If False, skip generation if the proto file hasn't been modified since the last generation.
    :return: True if we believe the code was generated successfully, or already exists.  False otherwise."""
    # Get the current directory
    current_dir = os.path.dirname(os.path.abspath(__file__))
    
    # Path to the proto file
    proto_file = os.path.join(current_dir, 'segmentation.proto')
    
    # Check if the proto file exists
    if not os.path.exists(proto_file):
        print(f"Error: Proto file not found at {proto_file}")
        return False

    # Figure out if the proto file was modified after the segmentation_pb2_grpc.py file
    if not force:
        pb2_grpc_file = os.path.join(current_dir, 'segmentation_pb2_grpc.py')
        if os.path.exists(pb2_grpc_file):
            proto_mtime = os.path.getmtime(proto_file)
            pb2_grpc_mtime = os.path.getmtime(pb2_grpc_file)
            if proto_mtime <= pb2_grpc_mtime:
                print("No changes detected in the proto file. Skipping code generation.")
                return True
    
    # Command to generate the gRPC code
    cmd = [
        sys.executable, '-m', 'grpc_tools.protoc',
        f'--proto_path={current_dir}',
        f'--python_out={current_dir}',
        f'--grpc_python_out={current_dir}',
        proto_file
    ]
    
    try:
        # Run the command
        subprocess.check_call(cmd)
        print(f"Successfully generated gRPC code from {proto_file}")
        
        # Fix imports in the generated files
        _fix_imports()
        
        return True
    except subprocess.CalledProcessError as e:
        print(f"Error generating gRPC code: {e}")
        return False
    except Exception as e:
        print(f"Unexpected error: {e}")
        return False

def _fix_imports():
    """Fix imports in the generated files to use the segmentation_grpc package."""
    current_dir = os.path.dirname(os.path.abspath(__file__))
    
    # Fix imports in segmentation_pb2_grpc.py
    pb2_grpc_file = os.path.join(current_dir, 'segmentation_pb2_grpc.py')
    if os.path.exists(pb2_grpc_file):
        with open(pb2_grpc_file, 'r') as f:
            content = f.read()
        
        # Replace 'import segmentation_pb2 as segmentation__pb2' with 'from segmentation_grpc import segmentation_pb2 as segmentation__pb2'
        content = content.replace(
            'import segmentation_pb2 as segmentation__pb2',
            'from segmentation_grpc import segmentation_pb2 as segmentation__pb2'
        )
        
        with open(pb2_grpc_file, 'w') as f:
            f.write(content)
        
        print(f"Fixed imports in {pb2_grpc_file}")

if __name__ == '__main__':
    generate_grpc_code()