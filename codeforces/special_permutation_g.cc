#include <algorithm>
#include <climits>
#include <cmath>
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
using std::gcd;
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
    int n;
    std::cin >> n;

    if (n < 4) {
      std::cout << -1 << "\n";
      continue;
    }

    // 1 3
    // 1 4 2 5
    // 3 1 4 2
    // 2 4 1 3
    // 2 4 1 3 5
    // 6 2 4 1 3 5
    // 6 2 4 1 3 5 7
    // 8 6 2 4 1 3 5 7
    // 8 6 2 4 1 3 5 7 9
    // 10 8 6 2 4 1 3 5 7 9
    // 10 8 6 2 4 1 3 5 7 9 11

    int l_even = (n % 2 == 0 ? n : n - 1);
    for (int i = l_even; i > 4; i -= 2) {
      std::cout << i << " ";
    }

    std::cout << "2 4 ";

    for (int i = 1; i <= n; i += 2) {
      std::cout << i << " ";
    }
    std::cout << "\n";
  }

  return 0;
}
