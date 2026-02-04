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

// 00100 1:0
// 10100 1:1
// 10101 2:1
// 11101 2:2
// 10111 2:2
// 11111 2:3 -- alice wins

// 10 1:0
// 01 1:0
// 11 2:0 -- bob wins

// Returns balance of scores. Asumming that we are at alice turn.
int balance(int zeroes) {
  if (zeroes == 1) {
    return 1;
  }

  if (zeroes % 2) {
    return -1;
  } else {
    return 1;
  }
}

int main(int argc, char* argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;
    string p;
    std::cin >> p;
    int zeroes =
        std::count_if(p.begin(), p.end(), [](char c) { return c == '0'; });

    int b = balance(zeroes);

    if (b == 0) {
      std::cout << "DRAW"
                << "\n";
    } else if (b < 0) {
      std::cout << "ALICE"
                << "\n";
    } else {
      std::cout << "BOB"
                << "\n";
    }
  }

  return 0;
}
