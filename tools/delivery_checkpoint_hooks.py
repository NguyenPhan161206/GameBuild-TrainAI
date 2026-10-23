"""In-process SDK extension; save only on the trainer thread, without editing SDK files."""
import os
import time
from pathlib import Path
from delivery_runs import snapshot


def install():
    from mlagents.trainers.model_saver.torch_model_saver import TorchModelSaver
    from mlagents.trainers.trainer.rl_trainer import RLTrainer
    from mlagents.trainers.trainer_controller import TrainerController
    save = TorchModelSaver.save_checkpoint
    advance = RLTrainer.advance
    not_done = TrainerController._not_done_training
    interval = float(os.environ.get('AEROLAB_CHECKPOINT_SECONDS', '300'))
    stop_step = int(os.environ.get('AEROLAB_STOP_AT_STEP', '0'))
    last = {}

    def verified_save(self, behavior_name, step):
        result = save(self, behavior_name, step)
        folder = Path(self.model_path).parent
        if (folder / 'run.json').exists():
            snapshot(folder, behavior_name, step, os.environ.get('AEROLAB_BACKUP_DIR'))
        last[id(self)] = time.monotonic()
        return result

    def periodic(self):
        result = advance(self)
        saver = self.model_saver
        previous = last.setdefault(id(saver), time.monotonic())
        if self.get_step > 0 and time.monotonic() - previous >= interval:
            self._checkpoint()
        return result

    TorchModelSaver.save_checkpoint = verified_save
    RLTrainer.advance = periodic
    def within_milestone(self):
        if stop_step and self.trainers and all(t.get_step>=stop_step for t in self.trainers.values()):
            return False
        return not_done(self)
    TrainerController._not_done_training = within_milestone
