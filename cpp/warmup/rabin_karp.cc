#include "cpp/warmup/rabin_karp.hpp"

#include <cstddef>
#include <cstdint>
#include <string>

namespace warmup {

/*
  Rabin-Karp algorithm searches for a pattern (patt) inside string
  S. It calculates hash value for every substring lenght of
  patt.size() in string S and run linear search only if hash mathes
  pattern hash.
*/

state::state(std::size_t mx) : maximal(mx) {
  for (std::size_t i = 1; i < mx; ++i) {
    left = left * x % modulo;
  }
}

void state::push_back(char c) {
  if (deq.size() == maximal) {
    pop();
  }

  hash = ((hash * x % modulo) + ctoi(c)) % modulo;

  deq.push_back(c);
}

void state::pop() {
  auto c = deq.front();
  deq.pop_front();

  // Avoiding negative hash.
  hash = (hash - ctoi(c) % modulo * left % modulo + modulo) % modulo;
}

bool search(const std::string& s, const std::string& pattern) {
  // EDGE_CASE: Every string contain empty substring.
  if (pattern.empty()) {
    return true;
  }

  // EDGE_CASE: Empty string cannot contain any non-empty substring.
  if (s.empty()) {
    return false;
  }

  int64_t pattern_hash = 0;
  for (auto c : pattern) {
    pattern_hash = ((pattern_hash * x % modulo) + ctoi(c)) % modulo;
  }

  auto st = state(pattern.size());

  for (std::size_t i = 0; i < s.size(); ++i) {
    st.push_back(s[i]);

    if (st.hash == pattern_hash && st.deq.size() == pattern.size()) {
      bool found = true;

      for (std::size_t j = 0; j < pattern.size(); ++j) {
        if (pattern[j] != s[i - (pattern.size() - 1) + j]) {
          found = false;
          break;
        }
      }

      if (found) {
        return true;
      }
    }
  }

  return false;
}

}  // namespace warmup
