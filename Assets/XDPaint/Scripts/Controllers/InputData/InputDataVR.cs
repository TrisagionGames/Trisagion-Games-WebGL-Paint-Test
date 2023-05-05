using UnityEngine;
using XDPaint.Controllers.InputData.Base;
using XDPaint.Core;
using XDPaint.Tools.Raycast;

namespace XDPaint.Controllers.InputData
{
    public class InputDataVR : InputDataBase
    {
        private Ray? ray;
        private Triangle triangle;
        private Transform penTransform;
        private Vector3 screenPoint = -Vector3.one;

        public override void Init(IPaintManager paintManager, Camera camera)
        {
            base.Init(paintManager, camera);
            penTransform = InputController.Instance.PenTransform;
        }

        public override void OnUpdate()
        {
            base.OnUpdate();
            ray = null;
            triangle = null;
        }

        protected override void OnHoverSuccess(Vector3 position, Triangle triangleData)
        {
            screenPoint = -Vector3.one;
            ray = new Ray(penTransform.position, penTransform.forward);
            triangle = RaycastController.Instance.Raycast(PaintManager, ray.Value);
            if (triangle != null)
            { 
                screenPoint = Camera.WorldToScreenPoint(triangle.WorldHit);
                base.OnHoverSuccess(screenPoint, triangle);
            }
            else
            {
                base.OnHoverFailed();
            }
        }

        protected override void OnDownSuccess(Vector3 position, float pressure = 1.0f)
        {
            IsOnDownSuccess = true;
            if (ray == null)
            {
                ray = new Ray(penTransform.position, penTransform.forward);
            }
            if (triangle == null)
            {
                triangle = RaycastController.Instance.Raycast(PaintManager, ray.Value);
            }
            if (triangle != null)
            {
                screenPoint = Camera.WorldToScreenPoint(triangle.WorldHit);
            }
            OnDownSuccessInvoke(screenPoint, pressure, triangle);
        }

        public override void OnPress(Vector3 position, float pressure = 1.0f)
        {
            if (!PaintManager.PaintObject.ProcessInput)
                return;
            
            if (IsOnDownSuccess)
            {
                if (ray == null)
                {
                    ray = new Ray(penTransform.position, penTransform.forward);
                }
                if (triangle == null)
                {
                    triangle = RaycastController.Instance.Raycast(PaintManager, ray.Value);
                }
                if (triangle != null)
                {
                    screenPoint = Camera.WorldToScreenPoint(triangle.WorldHit);
                }
                OnPressInvoke(screenPoint, pressure, triangle);
            }
        }
    }
}