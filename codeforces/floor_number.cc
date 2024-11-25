#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::max;
using std::min;
using std::pair;
using std::set;
using std::sort;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n, x;
    std::cin >> n >> x;

    if (n <= 2) {
      std::cout << 1 << "\n";
      continue;
    }

    n -= 2;
    std::cout << n / x + (n % x ? 1 : 0) + 1 << "\n";
  }
  return 0;
}
