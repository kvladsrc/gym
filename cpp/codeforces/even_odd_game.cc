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
    int n;
    std::cin >> n;

    vector<int64_t> a(n);
    for (auto& i : a) {
      std::cin >> i;
    }

    sort(a.begin(), a.end());

    int64_t alice_score = 0, bob_score = 0;
    bool alice_turn = true;

    while (!a.empty()) {
      int largest = a.back();
      a.pop_back();
      if (alice_turn) {
        if (largest % 2 == 0) {
          alice_score += largest;
        }
      } else {
        if (largest % 2 == 1) {
          bob_score += largest;
        }
      }
      alice_turn = !alice_turn;
    }

    if (alice_score == bob_score) {
      std::cout << "Tie"
                << "\n";
    } else if (alice_score > bob_score) {
      std::cout << "Alice"
                << "\n";
    } else {
      std::cout << "Bob"
                << "\n";
    }
  }

  return 0;
}
