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

  int t;
  std::cin >> t;
  while ((t--) != 0) {
    int64_t n, x, y;
    std::cin >> n >> x >> y;

    vector<int> a(n);
    for (auto& i : a) {
      std::cin >> i;
    }

    // The last digit in binary representation do not depends on
    // operation. Both operations have same effect on the digit.
    bool last = (x % 2) == 1;
    for (auto i : a) {
      bool a_last = (i % 2) == 1;
      last = last ^ a_last;
    }

    if (last == ((y % 2) == 1)) {
      std::cout << "Alice" << "\n";
    } else {
      std::cout << "Bob" << "\n";
    }
  }

  return 0;
}
