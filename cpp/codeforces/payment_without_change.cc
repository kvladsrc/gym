#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
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
    int a, b, n, S;
    std::cin >> a >> b >> n >> S;

    if (S / n <= a) {
      if (S % n <= b) {
        std::cout << "YES"
                  << "\n";
      } else {
        std::cout << "NO"
                  << "\n";
      }

      continue;
    }

    if (S - (a * n) <= b) {
      std::cout << "YES"
                << "\n";
      continue;
    }

    std::cout << "NO"
              << "\n";
  }

  return 0;
}
