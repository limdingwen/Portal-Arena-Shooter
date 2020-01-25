using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class AudioSet : MonoBehaviour
{
    public AudioClip[] clips;

    private AudioSource audioSource;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
    }

    public void PlayRandom()
    {
        audioSource.PlayOneShot(clips[Random.Range(0, clips.Length)]);
    }
}
