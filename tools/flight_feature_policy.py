"""Explicit feature selection for the clearance-teacher experiment.
Keeps goal X/Z, velocity X/Y/Z and altitude. Other first-layer columns are
zero and receive zero gradient; no scene/map information is introduced.
"""
import torch
CONTROL_FEATURES=(0,2,3,4,5,6)
def mask_actor(actor):
 layers=[m for m in actor.network_body.modules() if isinstance(m,torch.nn.Linear) and m.in_features==26]
 if len(layers)!=1:raise RuntimeError('Expected one 26-column flight input layer')
 weight=layers[0].weight;mask=torch.zeros_like(weight);mask[:,list(CONTROL_FEATURES)]=1
 with torch.no_grad():weight.mul_(mask)
 weight.register_hook(lambda gradient:gradient*mask)
 def after_load(module,incompatible):
  with torch.no_grad():weight.mul_(mask)
 actor.register_load_state_dict_post_hook(after_load)
def install():
 from mlagents.trainers.torch_entities.networks import SimpleActor
 original=SimpleActor.__init__
 def initialize(self,*args,**kwargs):
  original(self,*args,**kwargs);mask_actor(self)
 SimpleActor.__init__=initialize
