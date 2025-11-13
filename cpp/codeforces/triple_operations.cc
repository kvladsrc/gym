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

int main(int argc, char *argv[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t;
  std::cin >> t;
  while (t--) {
    int l, r;
    std::cin >> l >> r;
    int power = 1;
    int ops = 0;
    while (power <= l) {
      power *= 3;
      ops++;
    }
    int ops_l = ops;

    int res = ops;
    int cur = l;
    while (cur < r) {
      if (r < power) {
        res += (r - cur) * ops;
        cur = r;
        continue;
      }

      res += ((power - 1) - cur) * ops;
      cur = power - 1;
      ops++;
      power *= 3;
    }

    std::cout << res + ops_l << "\n";
  }

  return 0;
}
