# The segmentation server moved to Sam2Trainer

On 2026-11-20 the SAM2 segmentation server moved, with its history, to the Sam2Trainer repository:
<https://github.com/jamesra/Sam2SegmentationTrainer> (branch `master`), folder `SegmentationServer/`
(local checkout `D:\src\git\Sam2Trainer\SegmentationServer`). The last Viking commit holding it is `a599e705`.

It deploys as the same stack, `viking-segmentation`, now registered in Sam2Trainer's `deploy/stacks.psd1`
(connectomes-deploy reads it through `SAM2TRAINER_REPO_ROOT_HOST`).
