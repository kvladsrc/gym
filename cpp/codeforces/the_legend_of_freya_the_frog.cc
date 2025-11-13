#include <algorithm>
#include <climits>
#include <cmath>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <functional>
#include <iostream>
#include <map>
#include <memory>
#include <numeric>
#include <optional>
#include <queue>
#include <set>
#include <stack>
#include <string>
#include <tuple>
#include <unordered_map>
#include <utility>
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

int main(int /*argc*/, char* /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t = 0;
  std::cin >> t;
  while ((t--) != 0) {
    int x = 0;
    int y = 0;
    int k = 0;
    std::cin >> x >> y >> k;

    int x_jumps = (x / k) + ((x % k) == 0 ? 0 : 1);
    int y_jumps = (y / k) + ((y % k) == 0 ? 0 : 1);
    int res = 2 * (max(x_jumps, y_jumps));
    if (x_jumps > y_jumps) {
      res--;
    }

    std::cout << res << "\n";
  }

  return 0;
}
