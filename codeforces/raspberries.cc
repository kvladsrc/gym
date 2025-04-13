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

int main(int /*argc*/, char * /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t = 0;
  std::cin >> t;
  while ((t--) != 0) {
    int n = 0;
    int k = 0;
    std::cin >> n >> k;

    vector<int> a(n);
    for (auto &i : a) {
      std::cin >> i;
      i %= k;
    }

    int res = k;
    for (auto i : a) {
      int need = (k - i) % k;
      res = min(res, need);
    }

    // EDGE_CASE: a = {5, 5}, k = 4 -> make a = {6, 6};

    if (k == 4) {
      int ones = 0;
      int twos = 0;
      for (auto i : a) {
        if (i == 1) {
          ones++;
        }

        if (i == 2) {
          twos++;
        }
      }

      if (twos > 1) {
        res = 0;
      }

      if (twos == 1) {
        if (ones > 0) {
          res = min(res, 1);
        }

        res = min(res, 2);
      }

      if (ones > 1) {
        res = min(res, 2);
      }
    }

    std::cout << res << "\n";
  }

  return 0;
}
