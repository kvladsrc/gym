#include "cpp/warmup/boyer_moore.hpp"

#include <cstddef>
#include <map>
#include <string>

using std::map;
using std::string;

namespace warmup {

/*
 * The Boyer-Moore algorithm searches for the occurrence of the
 * pattern string (pattern) in the original string (s). If the pattern
 * is contained in the string as a substring, it returns true;
 * otherwise, it returns false.
 *
 * The algorithm maintains a variable (last) corresponding to the
 * position in the string that is aligned with the last character of
 * the pattern. If the characters at these positions do not match, the
 * algorithm shifts the position to the right. The shift is made by
 * the maximum possible distance to avoid missing any potential
 * occurrences. To achieve this, preprocessing is performed on the
 * pattern, and a table is created where each character in the pattern
 * is mapped to the rightmost position of its occurrence in the
 * pattern. In the event of a mismatch between s[last] and the last
 * character of the pattern, the variable last is increased so that
 * the pattern is shifted to align with the rightmost occurrence of
 * the character s[last] in the pattern. We use the rightmost index of
 * the character to ensure that no possible occurrences are
 * missed. All earlier occurrences are left for later consideration.
 */

void fill_last_seen(const string& pattern, map<char, std::size_t>& last_seen) {
  for (std::size_t i = 0; i < pattern.size(); ++i) {
    last_seen[pattern[i]] = i + 1;
  }
}

bool boyer_moore(const string& s, const string& pattern) {
  // EDGE_CASE: Every string contain empty substring.
  if (pattern.empty()) {
    return true;
  }

  // EDGE_CASE: Empty string cannot contain any non-empty substring.
  if (s.empty()) {
    return false;
  }

  map<char, std::size_t> last_seen;
  fill_last_seen(pattern, last_seen);

  auto last = pattern.size() - 1;

  while (last < s.size()) {
    // If the last char of pattern is equal to char on last position
    // then last_seen[c] will be equal to pattern.size.
    last += pattern.size() - last_seen[s[last]];

    bool found = true;

    for (std::size_t i = 0; i < pattern.size(); ++i) {
      if (pattern[i] != s[i + (last + 1) - pattern.size()]) {
        found = false;
        break;
      }
    }

    if (found) {
      return true;
    }

    last++;
  }

  return false;
}

}  // namespace warmup
