using System;
using UnityEngine;

namespace FrameSyncMoba.Bootstrap
{
    /// <summary>
    /// Presentation-facing application actions. These calls never mutate
    /// deterministic Gameplay directly; the endpoint application driver owns
    /// their network/application effects.
    /// </summary>
    public interface IClientUiActions
    {
        void SelectHero(int heroConfigId);
        void LockHero(int heroConfigId);
        void SetReady(bool isReady);
        void ReturnToMainMenu();
    }

    [DisallowMultipleComponent]
    public sealed class ClientUiActionRouter :
        MonoBehaviour,
        IClientUiActions
    {
        private Action<int> selectHero;
        private Action<int> lockHero;
        private Action<bool> setReady;
        private Action returnToMainMenu;

        public bool IsBound =>
            selectHero != null &&
            lockHero != null &&
            setReady != null &&
            returnToMainMenu != null;

        public void Bind(
            Action<int> selectHeroAction,
            Action<int> lockHeroAction,
            Action<bool> setReadyAction,
            Action returnToMainMenuAction)
        {
            selectHero = selectHeroAction ??
                throw new ArgumentNullException(
                    nameof(selectHeroAction));
            lockHero = lockHeroAction ??
                throw new ArgumentNullException(
                    nameof(lockHeroAction));
            setReady = setReadyAction ??
                throw new ArgumentNullException(
                    nameof(setReadyAction));
            returnToMainMenu =
                returnToMainMenuAction ??
                throw new ArgumentNullException(
                    nameof(returnToMainMenuAction));
        }

        public void SelectHero(int heroConfigId) =>
            Require(selectHero, nameof(SelectHero))(
                heroConfigId);

        public void LockHero(int heroConfigId) =>
            Require(lockHero, nameof(LockHero))(
                heroConfigId);

        public void SetReady(bool isReady) =>
            Require(setReady, nameof(SetReady))(
                isReady);

        public void ReturnToMainMenu() =>
            Require(
                returnToMainMenu,
                nameof(ReturnToMainMenu))();

        private static T Require<T>(
            T callback,
            string action)
            where T : Delegate
        {
            if (callback == null)
                throw new InvalidOperationException(
                    $"Client UI action {action} has no application owner.");
            return callback;
        }
    }
}
