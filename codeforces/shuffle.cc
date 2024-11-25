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

bool compare_pair(const pair<int, int> &a, const pair<int, int> &b) {
  if (a.second == b.second) {
    return a.first < b.first;
  }

  return a.second < b.second;
}

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n, m, x;
    std::cin >> n >> x >> m;
    int res = 1;
    int start = x, end = x;
    while (m--) {
      int l, r;
      std::cin >> l >> r;

      if (r < start || l > end) {
        continue;
      }

      int new_start = min(start, l);
      int new_end = max(end, r);
      res += start - new_start;
      res += new_end - end;
      start = new_start;
      end = new_end;
    }

    std::cout << res << "\n";
  }

  return 0;
}
