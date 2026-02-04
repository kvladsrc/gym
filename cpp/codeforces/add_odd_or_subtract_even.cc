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

int main(int argc, char* argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int a, b;
    std::cin >> a >> b;
    if (a < b && a % 2 != b % 2) {
      std::cout << 1 << "\n";
      continue;
    }

    if (a > b && a % 2 == b % 2) {
      std::cout << 1 << "\n";
      continue;
    }

    if (a == b) {
      std::cout << 0 << "\n";
      continue;
    }

    std::cout << 2 << "\n";
  }
  return 0;
}
