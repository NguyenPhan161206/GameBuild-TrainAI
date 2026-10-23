#!/usr/bin/env python3
"""SDK entrypoint with optional explicit flight input-column selection."""
import os
from delivery_checkpoint_hooks import install as install_checkpoints
install_checkpoints()
if os.environ.get('AEROLAB_FLIGHT_CONTROL_FEATURES')=='1':
 from flight_feature_policy import install
 install()
from mlagents.trainers.learn import main
main()
