#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <limits>
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
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int main(int argc, char* argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n, k;
    std::cin >> n >> k;

    if (n % 4 == 0) {
      std::cout << n / 2 << " ";
      std::cout << n / 4 << " ";
      std::cout << n / 4 << "\n";
      continue;
    }

    if (n % 2 == 1) {
      std::cout << n / 2 << " ";
      std::cout << n / 2 << " ";
      std::cout << 1 << "\n";
      continue;
    }

    // n = 2 * (2b + 1)
    std::cout << (n / 2) - 1 << " ";
    std::cout << (n / 2) - 1 << " ";
    std::cout << 2 << "\n";
  }

  return 0;
}
