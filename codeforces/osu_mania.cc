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
    size_t n = 0;
    std::cin >> n;

    vector<string> a(n);
    for (auto &i : a) {
      std::cin >> i;
    }

    std::reverse(a.begin(), a.end());
    for (auto s : a) {
      for (size_t idx = 0; idx < 4; ++idx) {
        if (s[idx] == '#') {
          std::cout << idx + 1 << " ";
          break;
        }
      }
    }
    std::cout << "\n";
  }

  return 0;
}
