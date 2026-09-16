// ============================================================================
// FloorExitDoor.cs
// ============================================================================
// PURPOSE:
//   Builds a freestanding medieval double door on slim grounded supports, with
//   imported leaves fitted to the colliders that swing before its trigger opens.
//   The doorway distinguishes collecting the last cake from deliberately leaving.
//   Explicit timing and crossing observations keep the transition reproducible.
// ARCHITECTURAL ROLE:
//   Sub-driver (§7e), owned by FloorDriver · Domain · Floor.
// KEY RESPONSIBILITIES:
//   - Keep visible door movement and physical passage in agreement.
//   - Fit rotated imported art in an aligned wrapper so width and depth stay correct.
//   - Fade a native Lumen threshold effect with the same opening progress.
//   - Prevent a stationary overlap from becoming an accidental floor transition.
// DEPENDENCIES:
//   - Core shared values and Floor-owned visual configuration only.
// USAGE NOTES:
//   Scene-owned through FloorDriver. Session supplies elapsed time; no Update loop.
//   No global settings. Reinitialization clears crossing and opening state.
// ============================================================================
using System;
using UnityEngine;
namespace Worsen.Domain.Floor
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class FloorExitDoor : MonoBehaviour
    {
        private readonly FloorExitDoorDriverState _state = new FloorExitDoorDriverState();
        private readonly FloorExitDoorPresenter _presenter = new FloorExitDoorPresenter();
        private FloorDriverConfig _config;
        public event Action<Collider> Contact;
        public bool FullyOpen => _state.FullyOpen;
        public bool Opening => _state.Opening;
        public void Configure(FloorDriverConfig config, Material wood, Material stone, Material seal)
        {
            _config = config;
            _state.Opening = false; _state.FullyOpen = false; _state.Elapsed = 0f; _state.LastClock = 0f; _state.Contacts.Clear();
            _state.Threshold = GetComponent<BoxCollider>();
            _state.Threshold.isTrigger = true;
            _state.Threshold.size = new Vector3(config.ExitSize.x, config.ExitSize.y, Mathf.Max(config.ExitSize.z, config.ExitCrossingDistance * 2f + 0.6f));
            _state.Threshold.center = Vector3.up * (config.ExitSize.y * 0.5f);
            _state.Threshold.enabled = false;
            float width = config.ExitSize.x, height = config.ExitSize.y, post = 0.14f;
            Block("Left Door Support", transform, new Vector3(-width*0.5f-post*0.5f,height*0.5f,0f),new Vector3(post,height,0.22f),wood);
            Block("Right Door Support", transform,new Vector3(width*0.5f+post*0.5f,height*0.5f,0f),new Vector3(post,height,0.22f),wood);
            Block("Door Crossbar",transform,new Vector3(0f,height+post*0.5f,0f),new Vector3(width+post*2f,post,0.22f),wood);
            Block("Left Grounded Foot",transform,new Vector3(-width*0.5f-post*0.5f,0.06f,0f),new Vector3(0.22f,0.12f,0.65f),stone);
            Block("Right Grounded Foot",transform,new Vector3(width*0.5f+post*0.5f,0.06f,0f),new Vector3(0.22f,0.12f,0.65f),stone);
            _state.LeftHinge = Hinge("Left Hinged Door",-1f,wood,seal);
            _state.RightHinge = Hinge("Right Hinged Door",1f,wood,seal);
            var keyStone = Block("Exit Seal",transform,new Vector3(0f,height+0.07f,-0.15f),new Vector3(0.2f,0.2f,0.07f),seal);
            keyStone.GetComponent<Collider>().enabled=false;
            var lightRoot=new GameObject("Exit Threshold Glow");lightRoot.transform.SetParent(transform,false);
            lightRoot.transform.localPosition=new Vector3(0f,height*0.75f,0.55f);
            _state.Glow=lightRoot.AddComponent<FloorLumenGlow>();
            _state.Glow.Configure(config.LumenExitGlowPrefab,Mathf.Max(width,height)*2f,config.ExitLockedColor,0.3f,true);
        }
        public void Open()
        {
            if (_state.Opening || _state.FullyOpen) return;
            _state.Opening = true; _state.Elapsed = 0f; _state.Contacts.Clear();
        }
        public void Tick(float clock)
        {
            if (_config == null || float.IsNaN(clock) || float.IsInfinity(clock)) return;
            float dt=Mathf.Max(0f,clock-_state.LastClock);_state.LastClock=clock;
            if (!_state.Opening || _state.FullyOpen) return;
            _state.Elapsed += dt;
            float progress=_presenter.OpeningProgress(_state.Elapsed,_config.ExitDoorOpeningDuration);
            float angle=_presenter.HingeAngle(progress,_config.ExitDoorOpeningAngle);
            _state.LeftHinge.localRotation=Quaternion.Euler(0f,-angle,0f);
            _state.RightHinge.localRotation=Quaternion.Euler(0f,angle,0f);
            _state.Glow.SetAppearance(Color.Lerp(_config.ExitLockedColor,_config.ExitOpenColor,progress),Mathf.Lerp(0.3f,2.5f,progress));
            if(progress>=1f)
            {
                _state.FullyOpen=true;_state.Opening=false;_state.Contacts.Clear();
                _state.Threshold.enabled=true;
            }
            Physics.SyncTransforms();
        }
        private Transform Hinge(string name,float side,Material wood,Material seal)
        {
            float half=_config.ExitSize.x*0.5f,height=_config.ExitSize.y;
            var root=new GameObject(name);root.transform.SetParent(transform,false);
            root.transform.localPosition=new Vector3(side*half,0f,0f);
            Vector3 center=new Vector3(-side*half*0.5f,height*0.5f,0f);
            var collision=Block("Door Leaf Collision",root.transform,center,new Vector3(half-0.015f,height,0.18f),wood);
            if(_config.ExitDoorPrefab!=null)
            {
                collision.GetComponent<Renderer>().enabled=false;
                // Fitting ratios are measured in hinge axes. Keep the fitting
                // parent aligned with those axes while imported art keeps its
                // required yaw; scaling the rotated mesh swaps width and depth.
                var fitRoot=new GameObject("Fitted Door Leaf");fitRoot.transform.SetParent(root.transform,false);
                var visual=Instantiate(_config.ExitDoorPrefab,fitRoot.transform,false);visual.name="Medieval Door Panel";
                foreach(var behavior in visual.GetComponentsInChildren<MonoBehaviour>(true))behavior.enabled=false;
                foreach(var animator in visual.GetComponentsInChildren<Animator>(true))animator.enabled=false;
                foreach(var collider in visual.GetComponentsInChildren<Collider>(true))collider.enabled=false;
                visual.transform.localRotation=Quaternion.Euler(0f,_config.ExitDoorPrefabYaw,0f);
                FitPanel(fitRoot.transform,root.transform,center,new Vector3(half-0.015f,height,0.18f));
            }
            else
            {
                for(int i=1;i<4;i++)
                    Block("Forged Iron Strap",root.transform,center+new Vector3(0f,(i-2)*height*0.28f,-0.11f),
                        new Vector3(half-0.03f,0.09f,0.045f),seal).GetComponent<Collider>().enabled=false;
            }
            return root.transform;
        }
        private static GameObject Block(string name,Transform parent,Vector3 position,Vector3 size,Material material)
        {
            var part=GameObject.CreatePrimitive(PrimitiveType.Cube);part.name=name;part.transform.SetParent(parent,false);
            part.transform.localPosition=position;part.transform.localScale=size;
            part.GetComponent<Renderer>().sharedMaterial=material;return part;
        }
        private static void FitPanel(Transform visual,Transform hinge,Vector3 targetCenter,Vector3 targetSize)
        {
            var renderers=visual.GetComponentsInChildren<Renderer>(true);
            if(renderers.Length==0)return;
            var bounds=LocalBounds(renderers,hinge);
            visual.localScale=Vector3.Scale(visual.localScale,new Vector3(targetSize.x/Mathf.Max(0.001f,bounds.size.x),
                targetSize.y/Mathf.Max(0.001f,bounds.size.y),targetSize.z/Mathf.Max(0.001f,bounds.size.z)));
            bounds=LocalBounds(renderers,hinge);
            visual.localPosition += targetCenter-bounds.center;
        }
        private static Bounds LocalBounds(Renderer[] renderers,Transform parent)
        {
            Bounds bounds=default;bool first=true;
            foreach(var renderer in renderers)
            {
                var local=renderer.localBounds;
                for(int corner=0;corner<8;corner++)
                {
                    Vector3 point=local.center+Vector3.Scale(local.extents,new Vector3((corner&1)==0?-1f:1f,(corner&2)==0?-1f:1f,(corner&4)==0?-1f:1f));
                    point=parent.InverseTransformPoint(renderer.transform.TransformPoint(point));
                    if(first){bounds=new Bounds(point,Vector3.zero);first=false;}else bounds.Encapsulate(point);
                }
            }
            return bounds;
        }
        private void Observe(Collider other)
        {
            if (!_state.FullyOpen || other == null) return;
            int key=other.GetInstanceID();
            if(!_state.Contacts.TryGetValue(key,out var crossing))
            { crossing=new FloorExitCrossingDriverState();_state.Contacts.Add(key,crossing); }
            Vector3 position=transform.InverseTransformPoint(other.bounds.center);
            if(_presenter.ObserveCrossing(crossing,position,_state.Threshold.size,_config.ExitCrossingDistance))
                Contact?.Invoke(other);
        }
        private void OnTriggerEnter(Collider other) => Observe(other);
        private void OnTriggerStay(Collider other) => Observe(other);
        private void OnTriggerExit(Collider other) { if(other!=null)_state.Contacts.Remove(other.GetInstanceID()); }
        private void OnDisable() => _state.Contacts.Clear();
    }
}
