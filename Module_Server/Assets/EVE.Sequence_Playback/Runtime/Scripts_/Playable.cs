using System.Collections;
using UnityEngine;

namespace EVE
{
    [ExecuteAlways]
    [System.Serializable]
    public abstract class Playable : MonoBehaviour
    {
        [HideInInspector]
        public bool playing = true;
        public double fps = 30;
        public LoopType loopType = default;
        public int maxFrames
        {
            get
            {
                return GetMaxFrames();
            }
        }
        protected bool direction = false;
        protected double _currentFrame = 0;
        public double currentFrame
        {
            get
            {
                return _currentFrame;
            }
            set
            {
                SetCurrentFrame(value);
            }
        }

        public abstract int GetMaxFrames();
        public virtual void SetCurrentFrameOnce(double value)
        {
            if (value < 0)
                value = 0;
            if (value < maxFrames)
            {
                var last = _currentFrame;
                _currentFrame = value;
                LoadFrame(last, value);
            }
            else
                Stop();
        }
        public virtual void SetCurrentFrameLoop(double value)
        {
            if (value >= maxFrames)
                value %= maxFrames;
            else if (value < 0)
                value += maxFrames;
            var last = _currentFrame;
            _currentFrame = value;
            LoadFrame(last, value);
        }
        public virtual void SetCurrentFramePingPong(double value)
        {
            if (value >= maxFrames)
            {
                value = 2 * maxFrames - value;
                direction = !direction;
            }
            else if (value < 0)
            {
                value = -value;
                direction = !direction;
            }
            var last = _currentFrame;
            _currentFrame = value;
            LoadFrame(last, value);
        }
        public virtual bool SetCurrentFrame(double value)
        {
            if (_currentFrame != value && maxFrames > 0)
            {
                switch (loopType)
                {
                    case LoopType.Once:
                        SetCurrentFrameOnce(value);
                        break;
                    case LoopType.Loop:
                        SetCurrentFrameLoop(value);
                        break;
                    case LoopType.PingPong:
                        SetCurrentFramePingPong(value);
                        break;
                }
                return true;
            }
            return false;
        }

        double startTime = 0f;
        public virtual void Update()
        {
            if (playing)
            {
                currentFrame = (Time.realtimeSinceStartupAsDouble - startTime) * fps;
            }
        }

        public virtual void OnFrameUpdate() { }
        public virtual void Play()
        {
            LoadFrame(-1, currentFrame);
            playing = true;
            startTime = Time.realtimeSinceStartupAsDouble;
            OnFrameUpdate();
        }
        public virtual void Play(double time)
        {
            StartCoroutine(_Play(time));
        }
        public virtual IEnumerator _Play(double time)
        {
            while (time > AudioSettings.dspTime) yield return null;
            Play();
        }
        public virtual void Pause()
        {
            playing = false;
            OnFrameUpdate();
        }
        public virtual void Stop()
        {
            playing = false;
            FirstFrame();
            OnFrameUpdate();
        }
        public virtual bool LastFrame()
        {
            if (maxFrames >= 1)
            {
                currentFrame = maxFrames - 1;
                OnFrameUpdate();
                return true;
            }
            return false;
        }
        public virtual void FirstFrame()
        {
            currentFrame = 0;
            OnFrameUpdate();
        }
        public virtual void NextFrame()
        {
            currentFrame = currentFrame + 1;
            OnFrameUpdate();
        }
        public virtual void PreviousFrame()
        {
            currentFrame = currentFrame - 1;
            OnFrameUpdate();
        }
        public virtual bool LoadFrame(double lastFrame, double newFrame)
        {
            return Mathf.FloorToInt((float)lastFrame) != Mathf.FloorToInt((float)newFrame);
        }
    }

    public enum LoopType
    {
        Once, Loop, PingPong
    }
}