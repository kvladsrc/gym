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
    int64_t n = 0;
    int64_t k = 0;
    std::cin >> n >> k;

    if (n % 2 == 0) {
      std::cout << "YES" << "\n";
      continue;
    }

    if (k % 2 == 0 || k > n) {
      std::cout << "NO" << "\n";
      continue;
    }

    std::cout << "YES" << "\n";
  }

  return 0;
}
