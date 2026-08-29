using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 사이드바에 표시되는 방문자 버튼들을 관리하는 컨트롤러
    /// VisitorManager의 이벤트를 구독하여 UI만 업데이트
    /// </summary>
    public class VisitorSideBarController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform buttonContainer;
        [SerializeField] private GameObject visitorButtonPrefab;
        
        [SerializeField] private SerializableDictionary<VisitorNPC, SideVisitorButton> activeButtons = new ();
        
        #region Unity Lifecycle

        // 캔버스(consistenceUI)가 꺼졌다 켜질 때마다 OnEnable/OnDisable이 호출된다.
        // OnEnable이 매니저 초기화보다 일렀던 경우를 대비해 Start에서도 보강한다 (TrySetup은 멱등).
        private void OnEnable()  => TrySetup();
        private void OnDisable() => UnsubscribeEvents();
        private void Start()     => TrySetup();

        private void OnDestroy()
        {
            UnsubscribeEvents();
            ClearAllButtons();
        }

        private void TrySetup()
        {
            if (VisitorManager.Instance == null) return;
            SubscribeEvents();
            ResyncButtons();
            // 재구독만으로는 '다음' 틱까지 게이지가 얼어붙으므로, 현재 값으로 즉시 한 번 다시 그린다.
            VisitorManager.Instance.ForceRefreshVisitorUI();
        }

        private void SubscribeEvents()
        {
            if (VisitorManager.Instance == null) return;

            var vm = VisitorManager.Instance;
            // -= 후 += 로 중복 구독을 방지한다 (멱등).
            vm.OnVisitorAdded               -= HandleVisitorAdded;
            vm.OnVisitorAdded               += HandleVisitorAdded;
            vm.OnVisitorRemoved             -= HandleVisitorRemoved;
            vm.OnVisitorRemoved             += HandleVisitorRemoved;
            vm.OnVisitorTimerUpdated        -= HandleTimerUpdated;
            vm.OnVisitorTimerUpdated        += HandleTimerUpdated;
            vm.OnVisitorWarningStateChanged -= HandleWarningStateChanged;
            vm.OnVisitorWarningStateChanged += HandleWarningStateChanged;
        }

        private void UnsubscribeEvents()
        {
            if (VisitorManager.Instance == null) return;

            VisitorManager.Instance.OnVisitorAdded               -= HandleVisitorAdded;
            VisitorManager.Instance.OnVisitorRemoved             -= HandleVisitorRemoved;
            VisitorManager.Instance.OnVisitorTimerUpdated        -= HandleTimerUpdated;
            VisitorManager.Instance.OnVisitorWarningStateChanged -= HandleWarningStateChanged;
        }

        /// <summary>
        /// 현재 방문자 목록과 버튼을 일치시킨다 (로드 복원·캔버스 재활성 대응).
        /// 파괴됐거나 떠나는 중이거나 목록에서 사라진 방문자의 버튼은 제거하고, 빠진 방문자의 버튼은 생성한다.
        /// </summary>
        private void ResyncButtons()
        {
            if (VisitorManager.Instance == null) return;

            var validVisitors = VisitorManager.Instance.ActiveVisitors;

            foreach (var key in activeButtons.Keys.ToList())
            {
                if (key != null && !key.isLeaving && validVisitors.Contains(key))
                    continue;

                if (activeButtons.TryGetValue(key, out SideVisitorButton button) && button != null)
                {
                    button.OnButtonClicked -= HandleButtonClicked;
                    Destroy(button.gameObject);
                }
                activeButtons.Remove(key);
            }

            // 입장 중/대기 중 방문자는 버튼을 만들지 않는다(슬롯/전령 포인트 도착자만).
            foreach (var visitor in validVisitors)
            {
                if (visitor != null && visitor.IsReadyForSidebar)
                    AddVisitorButton(visitor);
            }
        }

        #endregion
        
        #region Event Handlers
        
        private void HandleVisitorAdded(VisitorNPC visitor)
        {
            // OnVisitorAdded는 방문자가 상호작용 슬롯/전령 포인트에 도착해 준비됐을 때만 발행된다.
            // (입장 중·대기 중 방문자는 여기 오지 않는다)
            AddVisitorButton(visitor);
        }

        private void HandleVisitorRemoved(VisitorNPC visitor)
        {
            RemoveVisitorButton(visitor);
        }
        
        private void HandleTimerUpdated(VisitorNPC visitor, float remainingTime, float ratio)
        {
            if (activeButtons.TryGetValue(visitor, out SideVisitorButton button))
            {
                button.UpdateTimer(ratio);
            }
        }
        
        private void HandleWarningStateChanged(VisitorNPC visitor, bool isWarning)
        {
            if (activeButtons.TryGetValue(visitor, out SideVisitorButton button))
            {
                button.SetWarningState(isWarning);
            }
        }
        
        #endregion
        
        #region Button Management
        
        private void AddVisitorButton(VisitorNPC visitor)
        {
            if (visitor == null || visitor.isLeaving || activeButtons.ContainsKey(visitor))
                return;

            GameObject buttonObj = Instantiate(visitorButtonPrefab, buttonContainer);
            SideVisitorButton buttonItem = buttonObj.GetComponentOrNull<SideVisitorButton>();
            
            if (buttonItem == null)
            {
                Log.Error("VisitorSideBarController: VisitorButtonItem not found on prefab");
                Destroy(buttonObj);
                return;
            }
            
            buttonItem.Initialize(visitor);
            buttonItem.OnButtonClicked += HandleButtonClicked;
            
            activeButtons[visitor] = buttonItem;
        }
        
        private void RemoveVisitorButton(VisitorNPC visitor)
        {
            if (visitor == null || !activeButtons.TryGetValue(visitor, out SideVisitorButton button))
                return;

            if (button != null)
            {
                button.OnButtonClicked -= HandleButtonClicked;
                Destroy(button.gameObject);
            }
            
            activeButtons.Remove(visitor);
        }
        
        /// <summary>
        /// 모든 버튼 제거
        /// </summary>
        private void ClearAllButtons()
        {
            foreach (var kvp in activeButtons)
            {
                if (kvp.Value != null)
                {
                    kvp.Value.OnButtonClicked -= HandleButtonClicked;
                    Destroy(kvp.Value.gameObject);
                }
            }
            activeButtons.Clear();
        }
        
        private void HandleButtonClicked(VisitorNPC visitor)
        {
            if (visitor == null)
                return;
            
            visitor.OnInteractionButtonClicked();
        }
        
        #endregion
    }
}