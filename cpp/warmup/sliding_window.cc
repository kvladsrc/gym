#include "cpp/warmup/sliding_window.hpp"

#include <algorithm>
#include <string>

using std::max;
using std::string;

namespace warmup {

int find_max(string const& s, char c, int k) {
  int const n = s.size();
  int l = 0;
  int r = 0;
  int bk = 0;
  int mx = 0;

  if (n == 0) return 0;

  if (s[r] == c) {
    bk++;
  }

  while (r < n) {
    if (bk <= k) {
      mx = max(mx, r + 1 - l);
      r++;
      if (r < n && s[r] == c) {
        bk++;
      }
      continue;
    }

    if (s[l++] == c) {
      bk--;
    }
  }

  return mx;
}

}  // namespace warmup
