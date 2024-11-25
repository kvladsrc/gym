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
    int n, x, y;
    std::cin >> n >> x >> y;
    map<int, map<int, int>> m;
    int64_t res = 0;
    while (n--) {
      int buf;
      std::cin >> buf;
      res += m[(x - buf % x) % x][buf % y];
      m[buf % x][buf % y]++;
    }

    std::cout << res << "\n";
  }

  return 0;
}