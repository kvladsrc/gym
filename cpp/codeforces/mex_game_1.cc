#include <iostream>
#include <vector>

using std::vector;

int main(int argc, char* argv[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;

    vector<int> a(n + 1, 0);
    while (n--) {
      int buf;
      std::cin >> buf;
      a[buf]++;
    }

    // Bobs best option is to pick the last minimal value which is no
    // in c array.
    //
    // Hypothesis: Alica can pick every element with count >1. So Bob
    // best strategy is to pick second element with quntity 1.

    bool alice_pick = false;
    int res = 0;
    for (size_t idx = 0; idx < a.size(); ++idx) {
      if (a[idx] == 0) {
        res = idx;
        break;
      }

      if (a[idx] == 1) {
        if (alice_pick) {
          res = idx;
          break;
        }

        alice_pick = true;
      }
    }

    std::cout << res << "\n";
  }

  return 0;
}
