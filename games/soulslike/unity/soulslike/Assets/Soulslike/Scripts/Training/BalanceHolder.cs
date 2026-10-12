using UnityEngine;

namespace Soulslike.Training
{
    /// <summary>The balance a scene was built with (training/balance.json,
    /// put in by its builder): what a player without the file plays by
    /// (Balance).</summary>
    public class BalanceHolder : MonoBehaviour
    {
        [TextArea] public string json = "";
    }
}
