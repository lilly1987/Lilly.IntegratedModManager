using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace Lilly.IntegratedModManager
{
    internal sealed class ManagerFolderNameDialog : Window
    {
        private readonly string title;
        private readonly Func<string, bool> acceptAction;
        private string inputName;

        public override Vector2 InitialSize
        {
            get { return new Vector2(420f, 160f); }
        }

        public ManagerFolderNameDialog(
            string title,
            string initialName,
            Func<string, bool> acceptAction)
        {
            this.title = title;
            inputName = initialName ?? "";
            this.acceptAction = acceptAction;
            doCloseX = true;
            absorbInputAroundWindow = true;
            closeOnCancel = true;
            closeOnAccept = false;
        }

        public override void DoWindowContents(Rect rect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, rect.width, 32f), title);
            Text.Font = GameFont.Small;

            inputName = Widgets.TextField(
                new Rect(0f, 42f, rect.width, 32f), inputName);

            float buttonWidth = 120f;
            float buttonY = rect.height - 42f;
            if (Widgets.ButtonText(
                new Rect(rect.width / 2f - buttonWidth - 5f, buttonY, buttonWidth, 32f),
                "취소"))
            {
                Close();
            }

            if (Widgets.ButtonText(
                new Rect(rect.width / 2f + 5f, buttonY, buttonWidth, 32f),
                "확인"))
            {
                string trimmedName = inputName.Trim();
                if (trimmedName.NullOrEmpty())
                {
                    Messages.Message("이름을 입력하세요.", MessageTypeDefOf.RejectInput);
                    return;
                }

                if (acceptAction(trimmedName))
                    Close();
            }
        }
    }
}
