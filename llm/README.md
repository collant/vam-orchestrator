# LLM instructions

First install conda: https://www.anaconda.com/download/

### Create a new conda environment

```bash
conda create --name imposter
conda activate imposter
```


### Install Pytorch

Windows/Linux:

```
conda install pytorch torchvision torchaudio pytorch-cuda=11.7 -c pytorch -c nvidia
```

If you have any errors, please create a new conda environment and install Pytorch following this page: https://pytorch.org/get-started/locally/


### Install requirements

```bash
cd LLM
pip install -r requirements.txt
```

### Running

```bash
conda activate imposter
python run.py
```