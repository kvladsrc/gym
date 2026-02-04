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
    string n;
    std::cin >> n;

    if ((n[n.size() - 1] - '0') % 2 == 0) {
      std::cout << 0 << "\n";
      continue;
    }

    if ((n[0] - '0') % 2 == 0) {
      std::cout << 1 << "\n";
      continue;
    }

    bool has_even = std::any_of(n.begin(), n.end(),
                                [](char c) { return (c - '0') % 2 == 0; });

    if (has_even) {
      std::cout << 2 << "\n";
      continue;
    }

    std::cout << -1 << "\n";
  }

  return 0;
}
