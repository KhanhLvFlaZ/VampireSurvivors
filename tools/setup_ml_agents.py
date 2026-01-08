#!/usr/bin/env python3
"""
setup_ml_agents.py

Verifies environment requirements and initializes ml-agents training for the Vampire Survivors RL project.
Usage: python setup_ml_agents.py
"""

import sys
import subprocess
import os

def check_python_version():
    """Ensure Python 3.8+ is available (compatible with mlagents 1.0.0)."""
    major, minor = sys.version_info[:2]
    if major < 3 or (major == 3 and minor < 8):
        print(f"❌ Python 3.8+ required. Current: {major}.{minor}")
        sys.exit(1)
    if minor < 10:
        print(f"⚠ Python {major}.{minor} detected. Python 3.10+ recommended for best performance.")
    print(f"✓ Python {major}.{minor} OK")

def check_ml_agents():
    """Check if mlagents is installed."""
    try:
        import mlagents
        # Try to get version, but don't fail if not available
        try:
            version = mlagents.__version__
            print(f"✓ mlagents package installed (version {version})")
        except AttributeError:
            print("✓ mlagents package installed")
        return True
    except ImportError:
        print("❌ mlagents not installed")
        return False

def install_ml_agents():
    """Install ml-agents via pip."""
    print("\nInstalling mlagents (this may take a few minutes)...")
    subprocess.check_call([sys.executable, "-m", "pip", "install", "-U", "mlagents==1.0.0"])
    print("✓ mlagents installed")

def check_torch():
    """Check for PyTorch installation (recommended for ml-agents training)."""
    try:
        import torch
        print(f"✓ PyTorch installed (version {torch.__version__})")
    except ImportError:
        print("⚠ PyTorch not installed. ml-agents default CPU backend will be used.")
        print("  For GPU training, install PyTorch manually: https://pytorch.org/get-started/locally/")

def verify_project_structure():
    """Ensure expected directories exist."""
    required = ["Assets", "ProjectSettings", "ml-agents-configs"]
    missing = [d for d in required if not os.path.exists(d)]
    if missing:
        print(f"❌ Missing directories: {missing}")
        print("   Run this script from the project root: VampireSurvivors/")
        sys.exit(1)
    print("✓ Project structure verified")

def create_training_directories():
    """Create necessary training output directories."""
    dirs = ["results", "results/ppo_vampire", "models"]
    for d in dirs:
        os.makedirs(d, exist_ok=True)
    print("✓ Training directories created (results/, models/)")

def main():
    print("=== Vampire Survivors RL - ML-Agents Setup ===\n")

    check_python_version()
    verify_project_structure()

    # Check and install mlagents if missing
    if not check_ml_agents():
        choice = input("\nInstall mlagents now? (y/n): ").strip().lower()
        if choice == "y":
            install_ml_agents()
        else:
            print("Aborted. Install manually: pip install -U mlagents")
            sys.exit(0)

    check_torch()
    create_training_directories()

    print("\n=== Setup Complete ===")
    print("\nNext steps:")
    print("1. Open Unity project and ensure scene with RLSystem is configured")
    print("2. From this directory, run:")
    print("     mlagents-learn ml-agents-configs/ppo_vampire.yaml --run-id=ppo_vampire_001")
    print("3. Press Play in Unity when prompted by mlagents-learn")
    print("\nFor custom training or evaluation, see docs/RL_TRAINING_GUIDE.md\n")

if __name__ == "__main__":
    main()
