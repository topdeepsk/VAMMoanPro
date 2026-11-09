using System;
using UnityEngine;

// This is a VAMDeluxe plugin (see dollmaster)
// Just renamed it to avoid potential namespaces conflicts
// And slightly modified it to avoid triggers with own Autocolliders like the abdomen

namespace VAMMoanPlugin {

    public class TriggerEventArgs:EventArgs
    {
        public Collider collider { get; set; }
        public string evtType { get; set; }
    }
	
    public class CollideEventArgs:EventArgs
    {
        public Collision collision { get; set; }
        public string evtType { get; set; }
    }

    public class SexTriggerCollide :MonoBehaviour
    {
        TriggerEventArgs lastTriggerEvent;
        CollideEventArgs lastCollideEvent;

        public event EventHandler<TriggerEventArgs> OnTrigger;
        public event EventHandler<CollideEventArgs> OnCollide;

        void Awake()
        {
            lastTriggerEvent = new TriggerEventArgs
            {
                evtType = "none",
                collider = null
            };
			
            lastCollideEvent = new CollideEventArgs
            {
                evtType = "none",
                collision = null
            };
        }

        private void OnTriggerEnter(Collider other)
        {
            DoTriggerEvent("Entered", other);
        }

        private void OnTriggerExit(Collider other)
        {
            DoTriggerEvent("Exited", other);
        }
        
        private void OnCollisionEnter(Collision collision)
        {
            DoCollisionEvent("CEntered", collision);
			
        }
		
        private void OnCollisionExit(Collision collision)
        {
            DoCollisionEvent("CExited", collision);
        }
		
        private void DoTriggerEvent(string evtType,Collider col)
        {
            if (string.Equals(evtType, lastTriggerEvent.evtType) && col.gameObject == lastTriggerEvent.collider.gameObject)
            {
                return;
            }
            else
            {
				// Checking for triggers that are not allowed
				if( col.gameObject.name.Contains("FemaleAutoCollidersabdomen") ) {
					return;
				}
				
                TriggerEventArgs tempEvent = new TriggerEventArgs
                {
                    collider = col,
                    evtType = evtType
                };
                OnTriggerEvent(tempEvent);
                lastTriggerEvent = tempEvent;
            }
        }
		
        private void DoCollisionEvent(string evtType,Collision col)
        {
            if (string.Equals(evtType, lastCollideEvent.evtType) && col.gameObject == lastCollideEvent.collision.gameObject)
            {
                return;
            }
            else
            {
                CollideEventArgs tempEvent = new CollideEventArgs
                {
                    collision = col,
                    evtType = evtType
                };
                OnCollideEvent(tempEvent);
                lastCollideEvent = tempEvent;
            }
        }

        protected virtual void OnTriggerEvent(TriggerEventArgs e)
        {
            if (e == null) return;
            EventHandler<TriggerEventArgs> handler = OnTrigger;
            handler?.Invoke(this, e);            
        }
		
        protected virtual void OnCollideEvent(CollideEventArgs e)
        {
            if (e == null) return;
            EventHandler<CollideEventArgs> handler = OnCollide;
            handler?.Invoke(this, e);            
        }
        
    }
}