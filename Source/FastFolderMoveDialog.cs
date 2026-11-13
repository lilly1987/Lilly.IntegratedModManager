using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Lilly.IntegratedModManager
{
    internal sealed class FastFolderMoveDialog : Window
    {
        public sealed class FolderMoveTarget
        {
            public readonly string Title;
            public readonly string SourceName;
            public readonly ManagerCategoryData Category;
            public readonly ManagerTagData Tag;
            public readonly ManagerFolderData Folder;

            public FolderMoveTarget(
                string title,
                string sourceName,
                ManagerCategoryData category,
                ManagerTagData tag,
                ManagerFolderData folder)
            {
                Title = title;
                SourceName = sourceName;
                Category = category;
                Tag = tag;
                Folder = folder;
            }
        }

        private readonly List<List<FolderMoveTarget>> columns;
        private readonly Action<FolderMoveTarget> onTargetSelected;

        public override Vector2 InitialSize
        {
            get { return new Vector2(760f, 430f); }
        }

        public FastFolderMoveDialog(
            List<List<FolderMoveTarget>> columns,
            Action<FolderMoveTarget> onTargetSelected)
        {
            this.columns = columns ?? new List<List<FolderMoveTarget>>();
            this.onTargetSelected = onTargetSelected;
            doCloseX = true;
            absorbInputAroundWindow = true;
            closeOnCancel = true;
            closeOnAccept = false;
        }

        public override void DoWindowContents(Rect rect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, rect.width, 28f), "폴더 바로가기");
            Text.Font = GameFont.Small;

            float columnWidth = (rect.width - 30f) / Mathf.Max(1, columns.Count);
            float y = 40f;
            float listHeight = rect.height - 70f;

            for (int columnIndex = 0; columnIndex < columns.Count; columnIndex++)
            {
                List<FolderMoveTarget> targets = columns[columnIndex];
                float x = 10f + columnIndex * columnWidth;
                Rect columnRect = new Rect(x, y, columnWidth - 10f, listHeight);
                Widgets.DrawBoxSolid(columnRect, new Color(0.08f, 0.08f, 0.08f, 1f));

                if (targets != null && targets.Count > 0)
                {
                    Widgets.Label(new Rect(columnRect.x + 8f, columnRect.y + 8f,
                        columnRect.width - 16f, 24f), targets[0].SourceName);
                    float folderY = columnRect.y + 36f;
                    for (int i = 0; i < targets.Count; i++)
                    {
                        FolderMoveTarget target = targets[i];
                        Rect buttonRect = new Rect(columnRect.x + 8f, folderY, columnRect.width - 16f, 28f);
                        if (Widgets.ButtonText(buttonRect, target.Title))
                        {
                            if (onTargetSelected != null)
                                onTargetSelected(target);
                            Close();
                        }
                        folderY += 32f;
                    }
                }
                else
                {
                    Widgets.Label(new Rect(columnRect.x + 8f, columnRect.y + 8f,
                        columnRect.width - 16f, 24f), "폴더 없음");
                }
            }
        }
    }
}
