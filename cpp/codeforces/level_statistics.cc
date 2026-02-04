#include <algorithm>
#include <climits>
#include <cmath>
#include <cstdlib>
#include <ctime>
#include <iostream>
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

int main(int argc, char* argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;
    int plays = 0, clears = 0;
    bool res = true;
    for (int i = 0; i < n; ++i) {
      int cur_plays, cur_clears;
      std::cin >> cur_plays >> cur_clears;

      if (!res) continue;

      int change_plays = cur_plays - plays;
      int change_clears = cur_clears - clears;

      if ((change_clears > change_plays) ||
          (change_clears < 0 || change_plays < 0)) {
        res = false;
        continue;
      }

      plays = cur_plays;
      clears = cur_clears;
    }

    std::cout << (res ? "YES" : "NO") << "\n";
  }

  return 0;
}
