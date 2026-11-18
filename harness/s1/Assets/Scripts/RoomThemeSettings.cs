using System;
using UnityEngine;

namespace CatGame
{
    [Serializable]
    public sealed class RoomThemeDefinition
    {
        public string Id;
        public double MinimumTotalEarned;
        public string ResourceRoot;
    }

    [CreateAssetMenu(fileName="RoomThemeSettings", menuName="CatGame/Room growth themes")]
    public sealed class RoomThemeSettings : ScriptableObject
    {
        public const string ResourcePath="Settings/RoomThemeSettings";
        [Tooltip("Ordered cumulative earnings, never current wallet balance. Names are not shown to players.")]
        public RoomThemeDefinition[] Themes = {
            new RoomThemeDefinition { Id="starter", MinimumTotalEarned=0, ResourceRoot="Art/Room/Pixel/Growth/Starter" },
            new RoomThemeDefinition { Id="companions", MinimumTotalEarned=100000, ResourceRoot="Art/Room/Pixel/Growth/Companions" }
        };
        [Range(.1f,2)] public float TransitionSeconds=.6f;

        public void Validate()
        {
            if (Themes==null || Themes.Length==0 || Themes[0]==null || Themes[0].MinimumTotalEarned!=0)
                throw new InvalidOperationException("Room themes must start at zero earnings");
            var ids=new System.Collections.Generic.HashSet<string>();
            double previous=-1;
            foreach(var theme in Themes)
            {
                if(theme==null || string.IsNullOrEmpty(theme.Id) || !ids.Add(theme.Id) ||
                   string.IsNullOrEmpty(theme.ResourceRoot) || double.IsNaN(theme.MinimumTotalEarned) ||
                   double.IsInfinity(theme.MinimumTotalEarned) || theme.MinimumTotalEarned<=previous)
                    throw new InvalidOperationException("Invalid room theme identity, resource or threshold");
                previous=theme.MinimumTotalEarned;
            }
            if(float.IsNaN(TransitionSeconds) || float.IsInfinity(TransitionSeconds) || TransitionSeconds<=0)
                throw new InvalidOperationException("Invalid room transition duration");
        }

        public int Resolve(double totalEarned)
        {
            Validate();
            if(double.IsNaN(totalEarned) || double.IsInfinity(totalEarned) || totalEarned<0)
                throw new ArgumentOutOfRangeException(nameof(totalEarned));
            int selected=0;
            for(int i=1;i<Themes.Length && totalEarned>=Themes[i].MinimumTotalEarned;i++)selected=i;
            return selected;
        }
    }

    [Serializable] public sealed class RoomThemeLayout
    {
        public int width, height, cell;
        public float cornerX, cornerY, wallSlope;
        public RoomThemePlacement[] placements;
        public RoomThemeObstacle[] obstacles;
    }
    [Serializable] public sealed class RoomThemePlacement
    {
        public string asset, group;
        public float x,y,groundX,groundY;
        public bool wallDecor;
    }
    [Serializable] public sealed class RoomThemeObstacle
    {
        public float x,y,width,height;
        public Rect ToRect() { return new Rect(x,y,width,height); }
    }
}
