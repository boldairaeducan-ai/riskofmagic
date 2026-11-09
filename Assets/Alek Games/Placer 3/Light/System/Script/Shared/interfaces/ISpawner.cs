using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;

namespace AlekGames.Placer3.Shared
{
    public interface ISpawner
    {
        Task spawn();

    }
}