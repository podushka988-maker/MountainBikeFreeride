using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BikeGame
{
    /// <summary>
    /// Кнопки главного меню «Visual» (глаз) и «Close» (крестик): Visual прячет все панели и кнопки меню — видно только
    /// бота на трейле; Close возвращает меню как было (показывает ровно то, что было открыто). «Назад» на телефоне
    /// в режиме просмотра тоже возвращает меню. Создаёт MainMenuBuilder; заменяет TestButton.
    /// </summary>
    public class MenuViewToggle : MonoBehaviour
    {
        [Tooltip("Контейнер меню: Visual прячет всех его включённых детей.")]
        public RectTransform container;
        [Tooltip("Что ещё спрятать (например, окно выбора локации).")]
        public GameObject[] alsoHide;
        public Button visualButton, closeButton;

        private readonly List<GameObject> _hidden = new List<GameObject>();

        /// <summary>Сейчас режим просмотра (меню спрятано).</summary>
        public bool IsHidden { get; private set; }

        private void Start()
        {
            if (visualButton != null) visualButton.onClick.AddListener(Hide);
            if (closeButton != null) closeButton.onClick.AddListener(Show);
            SetButtons();
        }

        /// <summary>Visual: спрятать меню (запомнить, что было видно).</summary>
        public void Hide()
        {
            if (IsHidden) return;
            _hidden.Clear();
            if (container != null)
                foreach (Transform ch in container)
                    if (ch.gameObject.activeSelf && !IsButton(ch.gameObject)) _hidden.Add(ch.gameObject);
            if (alsoHide != null)
                foreach (GameObject g in alsoHide)
                    if (g != null && g.activeSelf && !_hidden.Contains(g)) _hidden.Add(g);
            foreach (GameObject g in _hidden) g.SetActive(false);
            IsHidden = true;
            SetButtons();
        }

        /// <summary>Close: вернуть меню как было.</summary>
        public void Show()
        {
            if (!IsHidden) return;
            foreach (GameObject g in _hidden) if (g != null) g.SetActive(true);
            _hidden.Clear();
            IsHidden = false;
            SetButtons();
        }

        private bool IsButton(GameObject g) =>
            (visualButton != null && g == visualButton.gameObject) || (closeButton != null && g == closeButton.gameObject);

        private void SetButtons()
        {
            if (visualButton != null) visualButton.gameObject.SetActive(!IsHidden);
            if (closeButton != null) closeButton.gameObject.SetActive(IsHidden);
        }
    }
}
