#include <cstddef>
#include <iostream>
#include <string>
#include <vector>

using std::string;
using std::vector;

namespace {

/*
 * Definitions:
 *
 * - A prefix of a string X is a string consisting of the first k
 * characters of X, where 0 ≤ k ≤ |X|.
 *
 * - A suffix of a string X is a string consisting of the last k
 * characters of X, where 0 ≤ k ≤ |X|.
 *
 * - A border of a string X is a string that is both a prefix and a
 * suffix of X but is not equal to X.
 *
 * Let l(X) be the longest border of string X, i.e., the longest
 * string that is both a prefix and a suffix of X and is not equal to
 * X.
 *
 * Lemmas:
 *
 * - The sequence l(X), l(l(X)), l(l(l(X))), ..., consists of strings,
 * each of which is both a prefix and a suffix of X.
 *
 * - Repeated application of l to X generates a sequence of strings,
 * each shorter than the previous one, ending with the empty string.
 *
 * - The set of all borders of X is exactly {l(X), l(l(X)), ..., ε},
 * where ε is the empty string.
 */

/*
 * Returns a vector a, a_i is the longest border length of pattern
 * suffix size i. Dynamic programming algorithm with O(n) complexity
 * based on the fact that a_i = a_j + 1, 1 <= j < i or 0;
 */
void compute_border_lengths(const string& pattern, vector<size_t>& borders) {
  borders.front() = 0;

  for (size_t idx = 1; idx < pattern.size(); ++idx) {
    auto len = borders[idx - 1];

    while (pattern[len] != pattern[idx] && len > 0) {
      len = borders[len - 1];
    }

    if (pattern[len] == pattern[idx]) {
      borders[idx] = len + 1;
    } else {
      borders[idx] = 0;
    }
  }
}

static bool kmp(const string& s, const string& pattern) {
  // EDGE_CASE: Every string contain empty substring.
  if (pattern.empty()) {
    return true;
  }

  // EDGE_CASE: Empty string cannot contain any non-empty substring.
  if (s.empty()) {
    return false;
  }

  vector<size_t> borders(pattern.size());
  compute_border_lengths(pattern, borders);
  size_t current_len = 0;

  for (char const c : s) {
    while (pattern[current_len] != c && (current_len > 0)) {
      current_len = borders[current_len - 1];
    }

    if (pattern[current_len] == c) {
      current_len++;
    } else {
      current_len = 0;
    }

    if (current_len == pattern.size()) {
      return true;
    }
  }

  return false;
}

}  // namespace

int main() {
  size_t patt_len = 0;
  size_t s_len = 0;
  string patt;
  string s;

  std::cin >> patt_len;
  if (patt_len != 0) {
    std::cin >> patt;
  }

  std::cin >> s_len;
  if (s_len != 0) {
    std::cin >> s;
  }

  if (kmp(s, patt)) {
    std::cout << "FOUND" << "\n";
  } else {
    std::cout << "NOT FOUND" << "\n";
  }
  return 0;
}
