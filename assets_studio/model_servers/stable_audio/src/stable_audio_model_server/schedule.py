"""A Stable Audio scheduler that can start from a lower noise level
(audio-to-audio)."""

from typing import Any

import torch
from diffusers import CosineDPMSolverMultistepScheduler


class PartialCosineScheduler(CosineDPMSolverMultistepScheduler):
    """The model's schedule, optionally starting at ``start_sigma``.

    All the requested steps run between ``start_sigma`` and the model's
    minimum, spaced as the model's own schedule (exponentially). The pipeline
    builds its start as ``noise * init_noise_sigma + encoded source``, so the
    source is noised exactly to the level the steps then remove.
    """

    start_sigma: float | None = None

    def set_timesteps(self, num_inference_steps: int | None = None, device: Any = None) -> None:
        super().set_timesteps(num_inference_steps, device)
        if self.start_sigma is not None:
            ramp = torch.linspace(0, 1, self.num_inference_steps)
            sigmas = self._compute_exponential_sigmas(ramp, sigma_max=self.start_sigma)
            sigmas = sigmas.to(dtype=torch.float32, device=device)
            self.timesteps = self.precondition_noise(sigmas)
            # The parent keeps its sigmas on the CPU; so do these.
            final = self.sigmas[-1:]
            self.sigmas = torch.cat([sigmas.to(final.device), final])

    @property
    def init_noise_sigma(self) -> float:
        if self.start_sigma is not None:
            return self.start_sigma
        return super().init_noise_sigma


def partial_scheduler(scheduler: Any) -> PartialCosineScheduler:
    """The same scheduler configuration, with the partial start."""
    return PartialCosineScheduler.from_config(scheduler.config)
