#include <algorithm>
#include <climits>
#include <cmath>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <iostream>
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

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    // Player forces another to decrease an element. Those who have 0
    // loses. Player cannot "give" element which where decreased to
    // opponent, so winning strategy is to always force opponent to
    // decrease minimal element until he reach 0.
    //
    // Let's find best element for Alice and Bob.
    int n;
    std::cin >> n;

    // Alice can chose minimal element from 2 to n. Bob can chose
    // between all except chosed by alice.
    vector<int> a(n);
    for (auto &i : a) {
      std::cin >> i;
    }

    sort(a.begin() + 1, a.end());
    a[0]--;
    int alice_choice = a[1];
    int bob_choice = a[0];
    if (n > 2) {
      bob_choice = min(bob_choice, a[2]);
    }

    if (alice_choice <= bob_choice) {
      std::cout << "Alice"
                << "\n";
    } else {
      std::cout << "Bob"
                << "\n";
    }
  }

  return 0;
}
