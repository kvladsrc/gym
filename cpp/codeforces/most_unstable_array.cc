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
    int n, m;
    std::cin >> n >> m;
    if (n == 1) {
      std::cout << 0 << "\n";
      continue;
    }

    if (n == 2) {
      std::cout << m << "\n";
      continue;
    }

    std::cout << m * 2 << "\n";
  }
  return 0;
}
