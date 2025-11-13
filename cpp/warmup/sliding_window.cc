#include <algorithm>
#include <iostream>
#include <string>

using std::max;
using std::string;

static int find_max(string const &s, char c, int k) {
  int const n = s.size();
  int l = 0;
  int r = 0;
  int bk = 0;
  int mx = 0;

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

int main(int /*argc*/, char * /*argv*/[]) {
  int n = 0;
  int k = 0;
  std::cin >> n >> k;
  string s;
  std::cin >> s;

  // s -- string consists only 'a' and 'b' chars. Need to find a
  // maximal lenth of consecutive subsequence of 'a' or 'b' chars
  // after changing no more than k chars.
  auto res = max(find_max(s, 'a', k), find_max(s, 'b', k));
  std::cout << res << "\n";

  return 0;
}
