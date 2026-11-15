#if DEVELOPMENT_BUILD && UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine;
using UnityEngine.EventSystems;

namespace CatGame
{
    // Observe only down/up; do not add drag handlers or change ScrollRect routing.
    internal sealed class AndroidPurchasePointerProbe : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        internal AndroidPurchaseTrace Trace;
        public void OnPointerDown(PointerEventData data) => Trace.Record("pointer-down");
        public void OnPointerUp(PointerEventData data) => Trace.Record("pointer-up");
    }
}
#endif
