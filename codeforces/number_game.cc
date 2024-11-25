#include <algorithm>
#include <cmath>
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
using std::sqrt;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

vector<int> list_turns(int n) {
  vector<int> res;

  if (n > 1) {
    res.push_back(n - 1);
  }

  for (int div = 1; (div * div) <= n; ++div) {
    if (n % div == 0) {
      if (div % 2 && div != 1) {
        res.push_back(n / div);
      }

      int buf = n / div;
      if (buf % 2 && buf != div) {
        res.push_back(n / buf);
      }
    }
  }

  return res;
}

// Returns true if first player wins the game.
bool play(int n) {
  if (n == 1) {
    return false;
  }

  // Optimization.
  if (n % 2) {
    // Divids by n, opponent got 1.
    return true;
  }

  auto possible_turns = list_turns(n);

  for (auto i : possible_turns) {
    if (!play(i)) {
      return true;
    }
  }

  return false;
}

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;

    if (play(n)) {
      std::cout << "Ashishgup"
                << "\n";
    } else {
      std::cout << "FastestFinger"
                << "\n";
    }
  }

  return 0;
}
