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

int size_to_int(string &a) {
  int res = 1;

  for (char c : a) {
    if (c == 'X') {
      res++;
      continue;
    }

    if (c == 'S') {
      res *= -1;
      break;
    }

    if (c == 'M') {
      res = 0;
      break;
    }
  }

  return res;
}

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    string a, b;
    std::cin >> a >> b;

    if (size_to_int(a) < size_to_int(b)) {
      std::cout << "<"
                << "\n";
      continue;
    }

    if (size_to_int(a) == size_to_int(b)) {
      std::cout << "="
                << "\n";
      continue;
    }

    std::cout << ">"
              << "\n";
  }

  return 0;
}
