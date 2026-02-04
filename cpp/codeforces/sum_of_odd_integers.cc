#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <numeric>
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
    int64_t n, k;
    std::cin >> n >> k;
    if (n < ((1 + (k * 2 - 1)) * k) / 2) {
      std::cout << "NO"
                << "\n";
      continue;
    }

    if (n % 2 != k % 2) {
      std::cout << "NO"
                << "\n";
      continue;
    }

    std::cout << "YES"
              << "\n";
  }
  return 0;
}
