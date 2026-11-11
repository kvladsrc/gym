using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Realms
{
    /// <summary>
    /// Clicks on the interface: buttons, rows, tiles and the minimap. UI Toolkit tells its
    /// elements what lies under the pointer from a cache that it renews only when the pointer
    /// moves to another pixel or the panel's layout changes, and a click it delivers to a stale
    /// element is lost: the players found the build and buy buttons on the cell card dead until
    /// the camera turned a little (2026-11-11). So the mouse is read here and the element under
    /// it is picked afresh when the button goes down and when it comes up; the click is the
    /// innermost element given an action with <see cref="OnPress"/>, and it runs when both ends
    /// of the click land on it.
    /// </summary>
    public sealed partial class Hud
    {
        readonly ConditionalWeakTable<VisualElement, Action> pressActions = new ConditionalWeakTable<VisualElement, Action>();
        VisualElement pressed;
        bool minimapHeld;
        Vector2 minimapPointer;
        int pressFrame = -10;

        /// <summary>Runs <paramref name="act"/> when the element, or anything in it, is clicked.</summary>
        void OnPress(VisualElement element, Action act)
        {
            pressActions.Remove(element);
            pressActions.Add(element, act);
        }

        /// <summary>A button that runs <paramref name="act"/> when clicked; it still lights up under the pointer and darkens while held.</summary>
        Button PressButton(Action act)
        {
            var button = new Button();
            OnPress(button, act);
            return button;
        }

        /// <summary>Reads the left mouse button over the interface; called first in every frame's update.</summary>
        void Presses()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || root?.panel == null)
                return;
            Vector2 screen = mouse.position.ReadValue();
            if (mouse.leftButton.wasPressedThisFrame)
            {
                VisualElement top = Picked(screen);
                pressed = PressableAt(top);
                minimapHeld = pressed == null && top != null && (top == minimap || minimap.Contains(top));
                if (minimapHeld)
                    MinimapAt(minimap.WorldToLocal(PanelPoint(screen)));
                minimapPointer = screen;
            }
            else if (minimapHeld && mouse.leftButton.isPressed && screen != minimapPointer)
            {
                MinimapAt(minimap.WorldToLocal(PanelPoint(screen)));
                minimapPointer = screen;
            }
            if (!mouse.leftButton.wasReleasedThisFrame)
                return;
            VisualElement down = pressed;
            pressed = null;
            minimapHeld = false;
            if (down == null || !down.enabledInHierarchy || PressableAt(Picked(screen)) != down
                || !pressActions.TryGetValue(down, out Action act))
                return;
            pressFrame = Time.frameCount;
            act?.Invoke();
        }

        /// <summary>The innermost element with a click action at or above <paramref name="top"/>.</summary>
        VisualElement PressableAt(VisualElement top)
        {
            for (VisualElement e = top; e != null; e = e.parent)
            {
                if (pressActions.TryGetValue(e, out _))
                    return e;
            }
            return null;
        }

        /// <summary>The top element under the pointer (screen pixels, origin bottom left), picked afresh.</summary>
        VisualElement Picked(Vector2 screen)
        {
            IPanel panel = root?.panel;
            return panel == null ? null : panel.PickAll(PanelPoint(screen), null);
        }

        Vector2 PanelPoint(Vector2 screen) =>
            RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(screen.x, Screen.height - screen.y));
    }
}
