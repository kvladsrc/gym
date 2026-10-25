"""Route camera estimation to synchronized scene footage, separate from pose input."""

import json
from pathlib import Path
from typing import Any


def preprocess(
    cfg: Any,
    demo: Any,
    camera_reference: Path | None = None,
    ignored_person_regions: list | None = None,
) -> None:
    original_vo = demo.SimpleVO
    original_tracker = getattr(demo, "Tracker", None)
    if original_tracker is not None:
        # The input contract is one foreground actor. A lost ByteTrack ID must
        # not freeze that actor's crop while a new ID follows the same person.
        class SinglePersonTracker(original_tracker):
            def get_one_track(self, video_path: str) -> Any:
                import torch
                from hmr4d.utils.net_utils import moving_average_smooth
                from person_tracking import dominant_person

                # Use detections rather than ByteTrack IDs: fencing fragments
                # IDs even while the same foreground person remains visible.
                history = []
                image_size = None
                for result in self.yolo.predict(
                    video_path, device="cuda", conf=0.5, classes=0, stream=True, verbose=False
                ):
                    image_size = (result.orig_shape[1], result.orig_shape[0])
                    history.append(
                        [{"id": 0, "bbx_xyxy": box} for box in result.boxes.xyxy.cpu().tolist()]
                    )
                boxes, report = dominant_person(
                    history, ignored_regions=ignored_person_regions or (), image_size=image_size
                )
                report.update(method="dominant-person-detections-v1")
                report.pop("track_ids")
                report.pop("tracklet_switches")
                Path(cfg.paths.bbx).with_name("person_tracking.json").write_text(json.dumps(report))
                result = torch.tensor(boxes, dtype=torch.float32)
                for _ in range(2):
                    result = moving_average_smooth(result, window_size=5, dim=0)
                return result

        demo.Tracker = SinglePersonTracker
    if camera_reference is not None:

        def camera_vo(_video: str, *args: Any, **kwargs: Any) -> Any:
            return original_vo(str(camera_reference), *args, **kwargs)

        demo.SimpleVO = camera_vo
    try:
        demo.run_preprocess(cfg)
    except AttributeError as error:
        if "matrix" not in str(error):
            raise
        raise RuntimeError(
            "Cannot estimate camera motion from this video: visual odometry found no "
            "usable camera pose. For segmented input provide camera_video with the "
            "original background. Use camera=static only for a stationary camera."
        ) from error
    finally:
        demo.SimpleVO = original_vo
        if original_tracker is not None:
            demo.Tracker = original_tracker
