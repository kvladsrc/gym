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
    int n = 0;
    std::cin >> n;

    /*
     * 4 4 4 4
     * 4 3 3 3
     * 4 3 2 2
     * 4 3 2 1
     *
     * Cannot be better. Prof: we cannot increse any number in the
     * table because every number larger can be found on position on
     * left and up positions.
     */

    int best_sum = 0;
    for (int s = 1; s <= n; ++s) {
      best_sum += (s + s - 1) * s;
    }
    std::cout << best_sum << " " << n * 2 << "\n";

    for (int ops = 1; ops <= n; ++ops) {
      std::cout << 1 << " " << ops << " ";
      for (int el = n; el > 0; --el) {
        std::cout << el << " ";
      }
      std::cout << "\n";

      std::cout << 2 << " " << ops << " ";
      for (int el = n; el > 0; --el) {
        std::cout << el << " ";
      }
      std::cout << "\n";
    }
  }

  return 0;
}
