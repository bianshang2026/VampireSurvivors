using UnityEngine;
using System.Collections;

namespace BiopunkRV
{
    /// <summary>
    /// 独立运行协程的组件，挂在特效GameObject上。
    /// 不依赖父物体（怪物被Destroy后仍能继续执行动画）。
    /// </summary>
    public class DeathEffectRunner : MonoBehaviour
    {
        private IEnumerator _routine;

        public void Run(IEnumerator routine)
        {
            _routine = routine;
            StartCoroutine(_routine);
        }

        private void Update() { }
    }
}
