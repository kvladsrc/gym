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
    int k = 0;
    std::cin >> n >> k;

    int cur = 0;
    int res = 0;
    for (int i = 0; i < n; ++i) {
      int buf = 0;
      std::cin >> buf;

      if (buf == 0 && cur > 0) {
        cur--;
        res++;
        continue;
      }

      if (buf >= k) {
        cur += buf;
      }
    }

    std::cout << res << "\n";
  }

  return 0;
}
