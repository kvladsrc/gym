#include <algorithm>
#include <climits>
#include <cmath>
#include <cstdint>
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
  int t;
  std::cin >> t;
  while (t--) {
    int64_t n, a, b;
    std::cin >> n >> a >> b;

    bool res = false;

    if (a == 1) {
      res = (n - 1) % b == 0;
      std::cout << (res ? "YES" : "NO") << "\n";
      continue;
    }

    int64_t power = 1;
    auto rem = n % b;
    while (power <= n) {
      if (rem == (power % b)) {
        res = true;
        break;
      }
      power *= a;
    }

    std::cout << (res ? "YES" : "NO") << "\n";
  }

  return 0;
}
