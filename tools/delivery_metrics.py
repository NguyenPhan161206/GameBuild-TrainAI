"""Operational metrics shared by resumable evaluations and study gates."""
import statistics

def summarize(reports,settings):
 n=len(reports);orders=sum(len(r['Orders']) for r in reports);delivered=sum(r['Delivered'] for r in reports)
 safe=lambda r:r['Result'] in ['Completed','Horizon'] and r['Delivered']>0 and r['SafeReturns']>0
 result=dict(settings)
 result.update(episodes=n,safe_complete_roundtrips=sum(r['Result']=='Completed' and r['Delivered']>=1 and r['SafeReturns']==r['Trips'] and r['Failed']==0 and r['Rejected']==0 for r in reports),
  operational_safe_episodes=sum(safe(r) for r in reports),outcomes={v:sum(r['Result']==v for r in reports) for v in sorted({r['Result'] for r in reports})},
  delivered=delivered,on_time=sum(r['OnTime'] for r in reports),total_orders=orders,
  safe_returns=sum(r['SafeReturns'] for r in reports),trips=sum(r['Trips'] for r in reports),
  rejected=sum(r['Rejected'] for r in reports),unfinished=sum(r['Unfinished'] for r in reports),failed=sum(r['Failed'] for r in reports),
  mean_energy=statistics.mean(r['Energy'] for r in reports),mean_distance=statistics.mean(r['Distance'] for r in reports),mean_elapsed=statistics.mean(r['Elapsed'] for r in reports),
  mean_delivery_latency=sum(r['MeanDeliveryLatency']*r['Delivered'] for r in reports)/delivered if delivered else None,
  energy_per_delivered=sum(r['Energy'] for r in reports)/delivered if delivered else None,
  on_time_rate=sum(r['OnTime'] for r in reports)/max(1,orders),deterministic_policy=True)
 for field,out in [('SourceSha256','source_sha256'),('FlightModelSha256','flight_model_sha256'),('DispatchModelSha256','dispatch_model_sha256')]:
  result[out]=sorted({r.get(field,'') for r in reports})
 critical=sum(result['outcomes'].get(k,0) for k in ['Collision','OutOfBounds','OutOfEnergy'])
 result['target_met']=(result['safe_complete_roundtrips']>=90 if settings['stage']==1 and n==100 and not settings['noisy'] else None)
 result['logistics_target_met']=(result['operational_safe_episodes']>=95 and critical==0 if settings['stage']==3 and n==100 else None)
 return result
