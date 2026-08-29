using UnityEngine;
using System.Collections.Generic;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 이펙트 관리 매니저
    /// 파티클과 효과음을 함께 재생하여 시각/청각 피드백 제공
    /// </summary>
    public class EffectManager : BaseManager<EffectManager>
    {
        [Header("Particle Prefabs")]
        [SerializeField] private GameObject namedAdventurerSpawnParticle;
        [SerializeField] private GameObject blacksmithSuccessParticle;
        [SerializeField] private GameObject blacksmithFailParticle;
        
        private List<GameObject> activeParticles = new List<GameObject>();
        
        #region 모험가 등장 이펙트
        
        /// <summary>
        /// 모험가 등급에 따른 등장 이펙트 재생
        /// </summary>
        public void PlayNamedAdventurerSpawnEffect(GameObject parent)
        {
            GameObject particlePrefab = namedAdventurerSpawnParticle;
            string soundName = "NamedAdventurer";

            // 파티클 재생 (캐릭터 뒤에 배치, 무한 반복)
            if (particlePrefab != null)
            {
                PlayParticle(particlePrefab, parent, 0f, 100);
            }
            
            // 효과음 재생
            if (!string.IsNullOrEmpty(soundName))
            {
                SoundManager.Instance?.PlaySFX(soundName);
            }
        }
        
        #endregion
        
        #region 강화/진화 이펙트

        /// <summary>
        /// 강화/진화 성공 이펙트 재생
        /// </summary>
        public void PlayBlacksmithSuccessEffect(GameObject parent)
        {
            if (blacksmithSuccessParticle != null)
                PlayParticle(blacksmithSuccessParticle, parent);

            SoundManager.Instance?.PlaySFX("BlacksmithSuccess");
        }

        /// <summary>
        /// 강화/진화 실패 이펙트 재생
        /// </summary>
        public void PlayBlacksmithFailEffect(GameObject parent)
        {
            if (blacksmithFailParticle != null)
                PlayParticle(blacksmithFailParticle, parent);

            SoundManager.Instance?.PlaySFX("BlacksmithFailed");
        }

        #endregion
        
        #region 리플레이 노드 이펙트

        /// <summary>
        /// 리플레이 시퀀스에서 노드 도달 시 이벤트 타입에 맞는 파티클 + SFX 재생
        /// </summary>
        public void PlayReplayNodeEffect(AdventureResultMotionConfig.NodeFXConfig fxConfig, Transform target)
        {
            if (fxConfig == null || target == null) return;

            Log.Info($"[EffectManager] Playing replay node effect for event: {fxConfig.eventType}");

            if (fxConfig.particlePrefab != null)
                PlayParticle(fxConfig.particlePrefab, target.gameObject);

            if (!string.IsNullOrEmpty(fxConfig.sfxKey))
                SoundManager.Instance?.PlaySFX(fxConfig.sfxKey);
        }

        #endregion

        #region 헬퍼 메서드

        /// <summary>
        /// 파티클 프리팹 인스턴스화 및 재생
        /// </summary>
        private void PlayParticle(GameObject particlePrefab, GameObject parent, float lifetime = 5f, int sortingOrder = 9999)
        {
            if (particlePrefab == null)
            {
                Log.Warn("[EffectManager] Particle prefab is null");
                return;
            }

            CleanupDestroyedParticles();

            GameObject particleObj = Instantiate(particlePrefab, parent.transform);
            particleObj.transform.localPosition = Vector3.zero;
            particleObj.transform.localRotation = Quaternion.identity;

            activeParticles.Add(particleObj);

            foreach (var r in particleObj.GetComponentsInChildren<Renderer>(true))
                r.sortingOrder = sortingOrder;

            // lifetime이 0이면 무한 반복재생 (프리팹의 Looping 설정에 의존, StopParticle로 명시적 종료)
            if (lifetime > 0f)
            {
                Destroy(particleObj, lifetime);
                StartCoroutine(RemoveParticleAfterDelay(particleObj, lifetime));
            }
        }

        /// <summary>
        /// 특정 부모 오브젝트에 재생 중인 파티클 중단
        /// </summary>
        public void StopParticle(GameObject parent)
        {
            CleanupDestroyedParticles();

            if (parent == null)
                return;

            // 역순으로 순회하며 해당 부모의 파티클만 제거
            for (int i = activeParticles.Count - 1; i >= 0; i--)
            {
                if (activeParticles[i] != null && activeParticles[i].transform.parent == parent.transform)
                {
                    Destroy(activeParticles[i]);
                    activeParticles.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// 부모가 파괴된 무한(lifetime=0) 파티클 등 죽은 참조를 리스트에서 청소한다.
        /// 방치하면 장시간 플레이에서 리스트가 무한 성장한다
        /// </summary>
        private void CleanupDestroyedParticles()
        {
            activeParticles.RemoveAll(p => p == null);
        }

        /// <summary>
        /// 모든 재생 중인 파티클 중단
        /// </summary>
        public void StopAllParticles()
        {
            CleanupDestroyedParticles();

            for (int i = activeParticles.Count - 1; i >= 0; i--)
            {
                if (activeParticles[i] != null)
                {
                    Destroy(activeParticles[i]);
                }
            }
            activeParticles.Clear();
            Log.Info("[EffectManager] Stopped all active particles");
        }

        /// <summary>
        /// 일정 시간 후 리스트에서 파티클 제거 (자동 파괴용)
        /// </summary>
        private System.Collections.IEnumerator RemoveParticleAfterDelay(GameObject particleObj, float delay)
        {
            yield return new WaitForSeconds(delay);
            
            // null이 아니면 리스트에서 제거 (이미 Destroy 호출되어 곧 파괴됨)
            if (activeParticles.Contains(particleObj))
            {
                activeParticles.Remove(particleObj);
            }
        }
        
        #endregion
    }
}
