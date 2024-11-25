#include <algorithm>
#include <climits>
#include <cmath>
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
using std::gcd;
using std::map;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::set;
using std::sort;
using std::sqrt;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int find_max(string &s, char c, int k) {
  int n = s.size();
  int l = 0, r = 0, bk = 0;
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

int main(int argc, char *argv[]) {
  int n, k;
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
