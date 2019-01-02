namespace CSLisp.Util
{
	/// <summary>
	/// Simple stream-like wrapper, into which we can add strings, and then peek or pop characters.
	/// </summary>
	public class InputStream 
	{
		/// <summary> Internal string storage </summary>
		private string _buffer;
		
		/// <summary> Current position in the buffer </summary>
		private int _index;
		
		/// <summary> Optional saved state </summary>
		private StreamState _saved;
		
		public InputStream() 
		{
			_buffer = "";
		}
		
		/// <summary> Appends more data to the stream </summary>
		public void Add (string str) {
			_buffer += str;
		}
		
		/// <summary> Returns true if empty, false if we still have characters in the buffer </summary>
		public bool IsEmpty => _index >= _buffer.Length;
		
		/// <summary> Returns the current character in the stream without removing it; 0 if empty. </summary>
		public char Peek () {
			return (_index >= _buffer.Length) ? (char)0 : _buffer[_index];
		}
		
		/// <summary> Returns and removes the current character in the stream; 0 if empty. </summary>
		public char Read () {
			char result = (char)0;
			if (_index < _buffer.Length) {
				result = _buffer[_index];
				_index++;
			}
			// if we reached end of the buffer, clear out internal storage
			if (_index >= _buffer.Length && _index > 0) {
				_buffer = "";
				_index = 0;
			}
			return result;
		}

        /// <summary> Saves the state of the stream into an internal register. Each save overwrites the previous one. </summary>
        public void Save () => _saved = new StreamState(_buffer, _index);

        /// <summary> 
		/// Restores (and deletes) a saved stream state, and returns true. 
		/// If one did not exist, it does not change existing state, and returns false.
		/// </summary>
        public bool Restore () {
			if (_saved == null) {
				return false;
			}
			
			this._index = _saved.index;
			this._buffer = _saved.buffer;
			this._saved = null;
			return true;
		}


        /// <summary>
        /// Used for storing and restoring buffer state
        /// </summary>
        private class StreamState
        {
            public int index;
            public string buffer;
            public StreamState (string buffer, int index) {
                this.index = index;
                this.buffer = buffer;
            }
        }
    }
}
