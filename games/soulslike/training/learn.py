"""mlagents-learn, with the trainer's update thread on the main thread's torch
device (train.sh runs this).

ML-Agents puts the networks on the GPU with torch.set_default_device, which
holds only in the thread that called it; with `threaded: true` the networks are
updated in a thread of their own, where new tensors then came out on the CPU
("Input type (torch.FloatTensor) and weight type (torch.cuda.FloatTensor)
should be the same"). The installed package is left as it is.
"""

import torch
from mlagents.torch_utils import default_device
from mlagents.trainers import trainer_controller
from mlagents.trainers.learn import main

_update = trainer_controller.TrainerController.trainer_update_func


def _update_on_device(self, trainer):
    device = default_device()
    if device.type == "cuda":
        torch.set_default_device(device.type)
    return _update(self, trainer)


trainer_controller.TrainerController.trainer_update_func = _update_on_device

if __name__ == "__main__":
    main()
