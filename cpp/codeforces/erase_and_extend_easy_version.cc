#include <algorithm>
#include <climits>
#include <cmath>
#include <cstddef>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <map>
#include <memory>
#include <numeric>
#include <queue>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::endl;
using std::map;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::queue;
using std::set;
using std::sort;
using std::sqrt;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

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

int main(int /*argc*/, char* /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  size_t n = 0;
  size_t k = 0;
  std::cin >> n >> k;

  string s;
  std::cin >> s;

  s += s;

  /*
   * 1. Remove suffix;
   * 2. Concatenate until res lenght < k;
   * 3. Remove suffix until lengh > k;
   */

  vector<size_t> spl(s.size());
  compute_border_lengths(s, spl);

  size_t best_pref_len = 1;

  for (size_t idx = 1; idx < s.size(); ++idx) {
    auto border = idx;
    auto best_local = border;

    while (border > 0) {
      if (s[best_local] > s[border]) {
        best_local = border;
      }
      border = spl[border - 1];
    }

    // s[0] case.
    if (s[best_local] > s[0]) {
      best_local = 0;
    }

    if (best_local == idx) {
      best_pref_len = idx + 1;
      continue;
    }

    best_pref_len = idx - best_local;
    break;
  }

  int cur = 0;
  string result;
  result.reserve(k);
  for (size_t i = 0; i < k; ++i) {
    result += s[cur];
    cur = (cur + 1) % best_pref_len;
  }
  std::cout << result << "\n";

  return 0;
}
