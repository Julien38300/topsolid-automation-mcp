#!/bin/bash
# Setup LoRA training environment in WSL2
# Usage: wsl bash scripts/setup-lora-env.sh
#
# Prerequisites: WSL2 with Ubuntu, NVIDIA GPU with CUDA drivers

set -e

ENV_DIR="$HOME/lora-env"
PYTHON_VERSION="python3.11"

echo "=== LoRA Environment Setup ==="
echo ""

# Check NVIDIA GPU
if ! command -v nvidia-smi &>/dev/null; then
    echo "ERROR: nvidia-smi not found. Install NVIDIA drivers for WSL2."
    echo "  See: https://docs.nvidia.com/cuda/wsl-user-guide/"
    exit 1
fi

echo "GPU detected:"
nvidia-smi --query-gpu=name,memory.total --format=csv,noheader
echo ""

# Check Python
if ! command -v $PYTHON_VERSION &>/dev/null; then
    echo "Installing $PYTHON_VERSION..."
    sudo apt update && sudo apt install -y $PYTHON_VERSION $PYTHON_VERSION-venv $PYTHON_VERSION-dev
fi

# Create venv
if [ -d "$ENV_DIR" ]; then
    echo "Environment already exists at $ENV_DIR"
    echo "  To recreate, run: rm -rf $ENV_DIR && bash $0"
else
    echo "Creating virtual environment at $ENV_DIR..."
    $PYTHON_VERSION -m venv "$ENV_DIR"
fi

# Activate and install
source "$ENV_DIR/bin/activate"

echo "Installing PyTorch with CUDA..."
pip install --upgrade pip
pip install torch torchvision torchaudio --index-url https://download.pytorch.org/whl/cu121

echo "Installing Unsloth + training dependencies..."
pip install "unsloth[colab-new] @ git+https://github.com/unslothai/unsloth.git"
pip install --no-deps trl peft accelerate bitsandbytes
pip install datasets transformers sentencepiece protobuf
pip install pyyaml  # For pipeline config

# Verify CUDA
echo ""
echo "Verifying CUDA..."
python -c "
import torch
print(f'PyTorch: {torch.__version__}')
print(f'CUDA available: {torch.cuda.is_available()}')
if torch.cuda.is_available():
    print(f'GPU: {torch.cuda.get_device_name(0)}')
    print(f'VRAM: {torch.cuda.get_device_properties(0).total_mem / 1024**3:.1f} GB')
else:
    print('WARNING: CUDA not available! Training will be very slow.')
"

echo ""
echo "=== Setup complete ==="
echo "  Activate with: source $ENV_DIR/bin/activate"
echo "  Train with:    cd /mnt/c/.../TopSolidMcpServer && python scripts/lora-pipeline.py --step train"
echo ""
