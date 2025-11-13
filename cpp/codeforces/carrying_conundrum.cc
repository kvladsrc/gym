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
#include <optional>
#include <queue>
#include <set>
#include <stack>
#include <string>
#include <tuple>
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

    int odd = 0;
    int even = 0;
    int power = 1;

    for (int i = n; i > 0; i /= 100) {
      odd += (i % 10) * power;
      power *= 10;
    }

    power = 1;
    for (int i = (n / 10); i > 0; i /= 100) {
      even += (i % 10) * power;
      power *= 10;
    }

    std::cout << ((odd + 1) * (even + 1)) - 2 << "\n";
  }

  return 0;
}
