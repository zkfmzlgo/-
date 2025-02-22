using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using FreeNet;
using UnityEngine.UI;

public class CPlayRoomUI : CSingletonMonobehaviour<CPlayRoomUI>, IMessageReceiver {

	// 원본 이미지들.
	Sprite back_image;


	// 각 슬롯의 좌표 객체.
	[SerializeField]
	Transform floor_slot_root;
	List<Vector3> floor_slot_position;

	[SerializeField]
	Transform deck_slot;

	[SerializeField]
	CPlayerCardPosition[] player_card_positions;


	// 카드 객체.
	List<CCardPicture> total_card_pictures;

	CCardCollision card_collision_manager;

	// 자리별 카드 스케일.
	readonly Vector3 SCALE_TO_FLOOR = new Vector3(0.8f, 0.8f, 0.8f);
	readonly Vector3 SCALE_TO_OTHER_HAND = new Vector3(0.5f, 0.5f, 0.5f);
	readonly Vector3 SCALE_TO_MY_HAND = new Vector3(1.0f, 1.0f, 1.0f);

	readonly Vector3 SCALE_TO_OTHER_FLOOR = new Vector3(0.6f, 0.6f, 0.6f);
	readonly Vector3 SCALE_TO_MY_FLOOR = new Vector3(0.6f, 0.6f, 0.6f);


	// 게임 플레이에 사용되는 객체들.
	byte player_me_index;
	List<CVisualFloorSlot> floor_ui_slots;
	// 가운데 쌓여있는 카드 객체.
	Stack<CCardPicture> deck_cards;
	List<CPlayerHandCardManager> player_hand_card_manager;
	// 플레이어가 먹은 카드 객체.
	List<CPlayerCardManager> player_card_manager;
	List<CPlayerInfoSlot> player_info_slots;

    CCardManager card_manager;

	Queue<CPacket> waiting_packets;


	// 효과 관련 객체들.
	GameObject ef_focus;


	// 테스트용 변수들.
	bool is_test_mode = false;
	byte test_auto_slot_index;

	public ImageController imageController;
	public CameraShake cameraShake;

	void Awake()
	{
		if (this.is_test_mode)
		{
			Time.timeScale = 100.0f;
		}

		CEffectManager.Instance.load_effects();

		this.waiting_packets = new Queue<CPacket>();
		this.card_collision_manager = GameObject.Find("GameManager").GetComponent<CCardCollision>();
		this.card_collision_manager.callback_on_touch = this.on_card_touch;

		this.player_me_index = 0;
		this.deck_cards = new Stack<CCardPicture>();
        this.card_manager = new CCardManager();
		this.card_manager.make_all_cards();
		this.floor_ui_slots = new List<CVisualFloorSlot>();
		for (byte i = 0; i < 12; ++i)
		{
			this.floor_ui_slots.Add(new CVisualFloorSlot(i, byte.MaxValue));
		}

		this.player_hand_card_manager = new List<CPlayerHandCardManager>();
		this.player_hand_card_manager.Add(new CPlayerHandCardManager());
		this.player_hand_card_manager.Add(new CPlayerHandCardManager());

		this.player_card_manager = new List<CPlayerCardManager>();
		this.player_card_manager.Add(new CPlayerCardManager());
		this.player_card_manager.Add(new CPlayerCardManager());

		this.player_info_slots = new List<CPlayerInfoSlot>();
		this.player_info_slots.Add(transform.Find("player_info_01").GetComponent<CPlayerInfoSlot>());
		this.player_info_slots.Add(transform.Find("player_info_02").GetComponent<CPlayerInfoSlot>());

		CPacketBufferManager.initialize(1);
		this.back_image = CSpriteManager.Instance.get_sprite("back");

		this.floor_slot_position = new List<Vector3>();
		make_slot_positions(this.floor_slot_root, this.floor_slot_position);


		// 카드 만들어 놓기.
		this.total_card_pictures = new List<CCardPicture>();
		GameObject original = Resources.Load("hwatoo") as GameObject;
		Vector3 pos = this.deck_slot.position;
		for (int i = 0; i < this.card_manager.cards.Count; ++i)
		{
			GameObject obj = GameObject.Instantiate(original);
			obj.transform.parent = transform;

			obj.AddComponent<CMovingObject>();
			CCardPicture card_pic = obj.AddComponent<CCardPicture>();
			this.total_card_pictures.Add(card_pic);

			//obj.GetComponent<Image>().color = back_red;
		}

		this.ef_focus = transform.Find("focus").gameObject;
		this.ef_focus.SetActive(false);

		load_hint_arrows();
	}


	void reset()
	{
		this.card_manager.make_all_cards();

		for (int i = 0; i < this.floor_ui_slots.Count; ++i)
		{
			this.floor_ui_slots[i].reset();
		}

		make_deck_cards();

		for (int i = 0; i < this.player_hand_card_manager.Count; ++i)
		{
			this.player_hand_card_manager[i].reset();
		}

		for (int i = 0; i < this.player_card_manager.Count; ++i)
		{
			this.player_card_manager[i].reset();
		}

		clear_ui();
	}


	void make_deck_cards()
	{
		CSpriteLayerOrderManager.Instance.reset();
		Vector3 pos = this.deck_slot.position;

		this.deck_cards.Clear();
		for (int i = 0; i < this.total_card_pictures.Count; ++i)
		{
			Animator ani = this.total_card_pictures[i].GetComponentInChildren<Animator>();
			ani.Play("card_idle");

			this.total_card_pictures[i].update_backcard(this.back_image);
			this.total_card_pictures[i].enable_collider(false);
			this.deck_cards.Push(this.total_card_pictures[i]);

			this.total_card_pictures[i].transform.localPosition = pos;
			pos.x -= 0.5f;
			pos.y += 0.5f;
			//pos.z -= 0.1f;
			this.total_card_pictures[i].transform.localScale = Vector3.one;
			this.total_card_pictures[i].transform.rotation = Quaternion.identity;

			this.total_card_pictures[i].sprite_renderer.sortingOrder = 
				CSpriteLayerOrderManager.Instance.Order;
		}
	}


	void make_slot_positions(Transform root, List<Vector3> targets)
	{
		Transform[] slots = root.GetComponentsInChildren<Transform>();
		for (int i = 0; i < slots.Length; ++i)
		{
			if (slots[i] == root)
			{
				continue;
			}

			targets.Add(slots[i].position);
		}
	}


	void Start()
	{
		if (imageController == null)
    {
        imageController = FindObjectOfType<ImageController>(); // 씬에서 첫 번째 ImageController를 찾음
        if (imageController == null)
        {
            Debug.LogError("⚠️ ImageController를 찾을 수 없습니다! 인스펙터에서 할당하거나 코드에서 할당해 주세요.");
        }
    }
		if (cameraShake == null)
    {
        cameraShake = FindObjectOfType<CameraShake>(); // 씬에서 첫 번째 ImageController를 찾음
        if (cameraShake == null)
        {
            Debug.LogError("⚠️ CameraShake 찾을 수 없습니다! 인스펙터에서 할당하거나 코드에서 할당해 주세요.");
        }
    }
		enter();
	}


	void clear_ui()
	{
		for (int i = 0; i < this.player_info_slots.Count; ++i)
		{
			this.player_info_slots[i].update_score(0);
			this.player_info_slots[i].update_go(0);
			this.player_info_slots[i].update_shake(0);
			this.player_info_slots[i].update_ppuk(0);
			this.player_info_slots[i].update_peecount(0);
		}
	}


	void move_card(CCardPicture card_picture, Vector3 begin, Vector3 to, float duration = 0.1f)
	{
		if (card_picture.card != null)
		{
			card_picture.update_image(get_hwatoo_sprite(card_picture.card));
		}
		else
		{
			card_picture.update_image(this.back_image);
		}

		CMovingObject mover = card_picture.GetComponent<CMovingObject>();
		mover.begin = begin;
		mover.to = to;
		mover.duration = duration;
		mover.run();
	}


	Sprite get_hwatoo_sprite(CCard card)
	{
		int sprite_index = card.number * 4 + card.position;
		return CSpriteManager.Instance.get_card_sprite(sprite_index);
	}


	IEnumerator distribute_cards(Queue<CCard> floor_cards, Dictionary<byte, Queue<CCard>> player_cards)
	{
		yield return new WaitForSeconds(1.0f);

		List<CCardPicture> begin_cards_picture = new List<CCardPicture>();

		// [바닥 -> 1P -> 2P 나눠주기] 를 두번 반복한다.
		for (int looping = 0; looping < 2; ++looping)
		{
			// 바닥에는 4장씩 분배한다.
			for (int i = 0; i < 4; ++i)
			{
				CCard card = floor_cards.Dequeue();
				CCardPicture card_picture = this.deck_cards.Pop();
				card_picture.update_card(card, get_hwatoo_sprite(card));
				begin_cards_picture.Add(card_picture);

				card_picture.transform.localScale = SCALE_TO_FLOOR;
				move_card(card_picture, card_picture.transform.position, this.floor_slot_position[i + looping * 4]);

				yield return new WaitForSeconds(0.02f);
			}

			yield return new WaitForSeconds(0.1f);

			// 플레어이의 카드를 분배한다.
			foreach(KeyValuePair<byte, Queue<CCard>> kvp in player_cards)
			{
				byte player_index = kvp.Key;
				Queue<CCard> cards = kvp.Value;

				byte ui_slot_index = (byte)(looping * 5);
				// 플레이어에게는 한번에 5장씩 분배한다.
				for (int card_index = 0; card_index < 5; ++card_index)
				{
					CCardPicture card_picture = this.deck_cards.Pop();
					card_picture.set_slot_index(ui_slot_index);
					this.player_hand_card_manager[player_index].add(card_picture);

					// 본인 카드는 해당 이미지를 보여주고,
					// 상대방 카드(is_nullcard)는 back_image로 처리한다.
					if (player_index == this.player_me_index)
					{
						CCard card = cards.Dequeue();
						card_picture.update_card(card, get_hwatoo_sprite(card));
						card_picture.transform.localScale = SCALE_TO_MY_HAND;
						move_card(card_picture, card_picture.transform.position,
							this.player_card_positions[player_index].get_hand_position(ui_slot_index));
					}
					else
					{
						card_picture.update_backcard(this.back_image);
						card_picture.transform.localScale = SCALE_TO_OTHER_HAND;
						move_card(card_picture, card_picture.transform.position,
							this.player_card_positions[player_index].get_hand_position(ui_slot_index));
					}

					++ui_slot_index;

					yield return new WaitForSeconds(0.02f);
				}
			}
		}

		sort_floor_cards_after_distributed(begin_cards_picture);
		sort_player_hand_slots(this.player_me_index);

		CPacket msg = CPacket.create((short)PROTOCOL.DISTRIBUTED_ALL_CARDS);
		CNetworkManager.Instance.send(msg);
	}


	Vector3 get_ui_slot_position(CVisualFloorSlot slot)
	{
		Vector3 position = this.floor_slot_position[slot.ui_slot_position];
		int stacked_count = slot.get_card_count();
		position.x += (stacked_count * 7.0f);
		position.y -= (stacked_count * 3.0f);
		return position;
	}


	void sort_floor_cards_after_distributed(List<CCardPicture> begin_cards_picture)
	{
		Dictionary<byte, byte> slots = new Dictionary<byte, byte>();

		for (byte i = 0; i < begin_cards_picture.Count; ++i)
		{
			byte number = begin_cards_picture[i].card.number;
			CVisualFloorSlot slot = this.floor_ui_slots.Find(obj => obj.is_same_card(number));
			Vector3 to = Vector3.zero;
			if (slot == null)
			{
				to = this.floor_slot_position[i];

				slot = this.floor_ui_slots[i];
				slot.add_card(begin_cards_picture[i]);
			}
			else
			{
				to = get_ui_slot_position(slot);

				slot.add_card(begin_cards_picture[i]);
			}


			Vector3 begin = this.floor_slot_position[i];
			move_card(begin_cards_picture[i], begin, to);
		}
	}


	void sort_floor_cards_when_finished_turn()
	{
		for (int i = 0; i < this.floor_ui_slots.Count; ++i)
		{
			CVisualFloorSlot slot = this.floor_ui_slots[i];
			if (slot.get_card_count() != 1)
			{
				continue;
			}

			CCardPicture card_pic = slot.get_first_card();
			move_card( card_pic, 
				card_pic.transform.position, 
				this.floor_slot_position[slot.ui_slot_position]);
		}
	}


	public void enter()
	{
		clear_ui();

		CNetworkManager.Instance.message_receiver = this;
        CNetworkManager.Instance.start_localserver();
		StartCoroutine(sequential_packet_handler());
	}


	void IMessageReceiver.on_recv(CPacket msg)
	{
		CPacket clone = new CPacket();
		msg.copy_to(clone);
		this.waiting_packets.Enqueue(clone);
	}


	/// <summary>
	/// 패킷을 순차적으로 처리하기 위한 루프.
	/// 카드 움직이는 연출 장면을 순서대로 처리하기 위해 구현한 매소드 이다.
	/// 코루틴에 의한 카드 이동 연출이 진행중일때도 서버로부터의 패킷은 수신될 수 있으므로
	/// 연출 도중에 다른 연출이 수행되는 경우가 생겨 버린다.
	/// 이런 경우를 방지하려면 두가지 방법이 있다.
	/// 첫번째. 각 연출 단계마다 다른 클라이언트들과 동기화를 수행한다.
	/// 두번째. 들어오는 패킷을 큐잉처리 하여 하나의 연출 장면이 끝난 뒤에 다음 패킷을 꺼내어 처리한다.
	/// 여기서는 두번째 방법으로 구현하였다.
	/// 첫번째 방법의 경우 동기화 패킷을 수시로 교환해야 하기 때문에 구현하기가 번거롭고
	/// 상대방의 네트워크 상태가 좋지 않을 경우 게임 진행이 매끄럽지 못하게 된다.
	/// </summary>
	/// <returns></returns>
	IEnumerator sequential_packet_handler()
	{
		while (true)
		{
			if (this.waiting_packets.Count <= 0)
			{
				yield return 0;
				continue;
			}

			CPacket msg = this.waiting_packets.Dequeue();
			PROTOCOL protocol = (PROTOCOL)msg.pop_protocol_id();

			switch (protocol)
			{
				case PROTOCOL.LOCAL_SERVER_STARTED:
					{
						CPacket send = CPacket.create((short)PROTOCOL.READY_TO_START);
						CNetworkManager.Instance.send(send);
					}
					break;

				case PROTOCOL.PLAYER_ORDER_RESULT:
					{
						reset();

						CUIManager.Instance.show(UI_PAGE.POPUP_PLAYER_ORDER);
						CPopupPlayerOrder popup =
							CUIManager.Instance.get_uipage(UI_PAGE.POPUP_PLAYER_ORDER).GetComponent<CPopupPlayerOrder>();
						popup.reset(this.back_image);
						popup.play();

						yield return new WaitForSeconds(2.6f);

						byte slot_count = msg.pop_byte();
						byte best_number = 0;
						byte head = 0;
						for (byte i = 0; i < slot_count; ++i)
						{
							byte slot_index = msg.pop_byte();
							byte number = msg.pop_byte();
							PAE_TYPE pae_type = (PAE_TYPE)msg.pop_byte();
							byte position = msg.pop_byte();

							CCard card = this.card_manager.find_card(number, pae_type, position);
							Debug.Log(string.Format("{0}, {1}, {2}", number, pae_type, position));
							popup.update_slot_info(slot_index, get_hwatoo_sprite(card));

							if (best_number < number)
							{
								head = slot_index;
								best_number = number;
							}

							yield return new WaitForSeconds(0.7f);
						}

						yield return new WaitForSeconds(0.5f);

						GameObject ef = CUIManager.Instance.get_uipage(UI_PAGE.POPUP_FIRST_PLAYER);
						if (head == 0)
						{
							ef.transform.localPosition = new Vector3(100, 100, 0);
						}
						else
						{
							ef.transform.localPosition = new Vector3(100, -100, 0);
						}
						CUIManager.Instance.show(UI_PAGE.POPUP_FIRST_PLAYER);

						yield return new WaitForSeconds(1.5f);
						CUIManager.Instance.hide(UI_PAGE.POPUP_PLAYER_ORDER);
						CUIManager.Instance.hide(UI_PAGE.POPUP_FIRST_PLAYER);
					}
					break;

				case PROTOCOL.BEGIN_CARD_INFO:
					{
						if (is_test_mode)
						{
							this.test_auto_slot_index = 0;
						}

						Queue<CCard> floor_cards = new Queue<CCard>();
						// floor cards.
						this.player_me_index = msg.pop_byte();
						byte floor_count = msg.pop_byte();
						for (byte i = 0; i < floor_count; ++i)
						{
							byte number = msg.pop_byte();
							PAE_TYPE pae_type = (PAE_TYPE)msg.pop_byte();
							byte position = msg.pop_byte();

							CCard card = this.card_manager.find_card(number, pae_type, position);
							if (card == null)
							{
								Debug.LogError(string.Format("Cannot find the card. {0}, {1}, {2}",
									number, pae_type, position));
							}
							floor_cards.Enqueue(card);
						}


						Dictionary<byte, Queue<CCard>> player_cards = new Dictionary<byte, Queue<CCard>>();
						byte player_count = msg.pop_byte();
						for (byte player = 0; player < player_count; ++player)
						{
							Queue<CCard> cards = new Queue<CCard>();
							byte player_index = msg.pop_byte();
							byte card_count = msg.pop_byte();
							for (byte i = 0; i < card_count; ++i)
							{
								byte number = msg.pop_byte();
								if (number != byte.MaxValue)
								{
									PAE_TYPE pae_type = (PAE_TYPE)msg.pop_byte();
									byte position = msg.pop_byte();
									CCard card = this.card_manager.find_card(number, pae_type, position);
									cards.Enqueue(card);
								}
							}

							player_cards.Add(player_index, cards);
						}


						yield return StartCoroutine(distribute_cards(floor_cards, player_cards));
					}
					break;

				case PROTOCOL.START_TURN:
					{
						byte remain_bomb_card_count = msg.pop_byte();
						refresh_hint_mark();

						if (this.is_test_mode)
						{
							if (this.player_hand_card_manager[0].get_card_count() <= 0)
							{
								break;
							}

							CPacket card_msg = CPacket.create((short)PROTOCOL.SELECT_CARD_REQ);
							CCardPicture card_pic = this.player_hand_card_manager[0].get_card(0);

							card_msg.push(card_pic.card.number);
							card_msg.push((byte)card_pic.card.pae_type);
							card_msg.push(card_pic.card.position);
							card_msg.push(this.test_auto_slot_index);
							++this.test_auto_slot_index;

							CNetworkManager.Instance.send(card_msg);
						}
						else
						{
							// 내 차례가 되었을 때 카드 선택 기능을 활성화 시켜준다.
							this.ef_focus.SetActive(true);
							this.card_collision_manager.enabled = true;
							this.player_hand_card_manager[0].enable_all_colliders(true);

							// 이전에 폭탄낸게 남아있다면 가운데 카드를 뒤집을 수 있도록 충돌박스를 켜준다.
							if (remain_bomb_card_count > 0)
							{
								CCardPicture top_card = deck_cards.Peek();
								top_card.enable_collider(true);

								show_hint_mark(top_card.transform.position);
							}
						}
					}
					break;

				case PROTOCOL.SELECT_CARD_ACK:
					yield return StartCoroutine(on_select_card_ack(msg));
					break;

				case PROTOCOL.FLIP_DECK_CARD_ACK:
					yield return StartCoroutine(on_flip_deck_card_ack(msg));
					break;

				case PROTOCOL.TURN_RESULT:
					{
						// 데이터 파싱 시작 ----------------------------------------
						byte player_index = msg.pop_byte();
						yield return StartCoroutine(on_turn_result(player_index, msg));
					}
					break;

				case PROTOCOL.ASK_GO_OR_STOP:
					CUIManager.Instance.show(UI_PAGE.POPUP_GO_STOP);
					break;

				case PROTOCOL.UPDATE_PLAYER_STATISTICS:
					update_player_statistics(msg);
					break;

				case PROTOCOL.ASK_KOOKJIN_TO_PEE:
					CUIManager.Instance.show(UI_PAGE.POPUP_ASK_KOOKJIN);
					break;

				case PROTOCOL.MOVE_KOOKJIN_TO_PEE:
					{
						byte player_index = msg.pop_byte();
						yield return StartCoroutine(move_kookjin_to_pee(player_index));
					}
					break;

				case PROTOCOL.NOTIFY_GO_COUNT:
					{
                        byte delay = msg.pop_byte();
						byte go_count = msg.pop_byte();

                        yield return StartCoroutine(delay_if_exist(delay));
						yield return StartCoroutine(show_go_count(go_count));
					}
					break;

				case PROTOCOL.GAME_RESULT:
					yield return StartCoroutine(on_game_result(msg));
					break;
			}

			yield return 0;
		}
	}


    IEnumerator delay_if_exist(byte delay)
    {
        if (delay > 0)
        {
            yield return new WaitForSeconds(delay);
        }
    }


	IEnumerator show_go_count(byte count)
	{
		CUIManager.Instance.show(UI_PAGE.POPUP_GO_COUNT);
		CUIManager.Instance.get_uipage(UI_PAGE.POPUP_GO_COUNT).GetComponent<CPopupGo>().refresh(count);

		yield return new WaitForSeconds(1.0f);
		CUIManager.Instance.hide(UI_PAGE.POPUP_GO_COUNT);
	}


	IEnumerator on_game_result(CPacket msg)
	{
		byte is_win = msg.pop_byte();
		short money = msg.pop_int16();
		short score = msg.pop_int16();
		short double_val = msg.pop_int16();
		short final_score = msg.pop_int16();

		CUIManager.Instance.show(UI_PAGE.POPUP_STOP);
		yield return new WaitForSeconds(2.0f);

		CUIManager.Instance.hide(UI_PAGE.POPUP_STOP);

		CUIManager.Instance.show(UI_PAGE.POPUP_GAME_RESULT);
		CPopupGameResult popup = 
			CUIManager.Instance.get_uipage(UI_PAGE.POPUP_GAME_RESULT).GetComponent<CPopupGameResult>();
		popup.refresh(is_win, money, score, double_val, final_score);
	}


	IEnumerator move_kookjin_to_pee(byte player_index)
	{
		CCardPicture card_picture =
			this.player_card_manager[player_index].get_card(8, PAE_TYPE.YEOL, 0);

		// 카드 자리 움직이기.
		move_card(card_picture, card_picture.transform.position, 
			get_player_card_position(player_index, PAE_TYPE.PEE));

		// 열끗에서 지우고 피로 넣는다.
		this.player_card_manager[player_index].remove(card_picture);

		card_picture.card.change_pae_type(PAE_TYPE.PEE);
		card_picture.card.set_card_status(CARD_STATUS.TWO_PEE);

		this.player_card_manager[player_index].add(card_picture);

		yield return new WaitForSeconds(1.0f);

		// 바닥 패 정렬.
		refresh_player_floor_slots(PAE_TYPE.YEOL, player_index);
		refresh_player_floor_slots(PAE_TYPE.PEE, player_index);
	}


	void update_player_statistics(CPacket msg)
	{
		byte player_index = msg.pop_byte();
		short score = msg.pop_int16();
		byte go_count = msg.pop_byte();
		byte shaking_count = msg.pop_byte();
		byte ppuk_count = msg.pop_byte();
		byte pee_count = msg.pop_byte();

		this.player_info_slots[player_index].update_score(score);
		this.player_info_slots[player_index].update_go(go_count);
		this.player_info_slots[player_index].update_shake(shaking_count);
		this.player_info_slots[player_index].update_ppuk(ppuk_count);
		this.player_info_slots[player_index].update_peecount(pee_count);
	}


	List<CCard> parse_cards_to_get(CPacket msg)
	{
		List<CCard> cards_to_give = new List<CCard>();
		byte count_to_give = msg.pop_byte();
		//Debug.Log(string.Format("================== count to give. {0}", count_to_give));
		for (int i = 0; i < count_to_give; ++i)
		{
			byte card_number = msg.pop_byte();
			PAE_TYPE pae_type = (PAE_TYPE)msg.pop_byte();
			byte position = (byte)msg.pop_byte();
			CCard card = this.card_manager.find_card(card_number, pae_type, position);
			cards_to_give.Add(card);
			//Debug.Log(string.Format("{0}, {1}, {2}", card_number, pae_type, position));
		}

		return cards_to_give;
	}


	List<CCardPicture> parse_cards_to_take_from_others(byte player_index, CPacket msg)
	{
		// 뺏어올 카드.
		List<CCardPicture> take_cards_from_others = new List<CCardPicture>();
		byte victim_count = msg.pop_byte();
		for (byte victim = 0; victim < victim_count; ++victim)
		{
			byte victim_index = msg.pop_byte();
			byte count_to_take = msg.pop_byte();
			for (byte i = 0; i < count_to_take; ++i)
			{
				byte card_number = msg.pop_byte();
				PAE_TYPE pae_type = (PAE_TYPE)msg.pop_byte();
				byte position = (byte)msg.pop_byte();

				CCardPicture card_pic = this.player_card_manager[victim_index].get_card(
					card_number, pae_type, position);
				take_cards_from_others.Add(card_pic);
				this.player_card_manager[victim_index].remove(card_pic);
			}
		}

		short score = msg.pop_int16();
		byte remain_bomb_card_count = msg.pop_byte();

		// UI적용.
		this.player_info_slots[player_index].update_score(score);

		return take_cards_from_others;
	}


	IEnumerator on_turn_result(byte player_index, CPacket msg)
	{
		List<CCard> cards_to_give = parse_cards_to_get(msg);
		List<CCardPicture> take_cards_from_others = parse_cards_to_take_from_others(player_index, msg);

		yield return StartCoroutine(move_after_flip_card(player_index, take_cards_from_others, cards_to_give));
	}


	IEnumerator on_select_card_ack(CPacket msg)
	{
		// 데이터 파싱 시작 ----------------------------------------
        byte delay = msg.pop_byte();
		byte player_index = msg.pop_byte();

		// 카드 내는 연출을 위해 필요한 변수들.
		CARD_EVENT_TYPE card_event = CARD_EVENT_TYPE.NONE;
		List<CCard> bomb_cards_info = new List<CCard>();
		List<CCard> shaking_cards_info = new List<CCard>();
		byte slot_index = byte.MaxValue;
		byte player_card_number = byte.MaxValue;
		PAE_TYPE player_card_pae_type = PAE_TYPE.PEE;
		byte player_card_position = byte.MaxValue;

		// 플레이어가 낸 카드 정보.
		player_card_number = msg.pop_byte();
		player_card_pae_type = (PAE_TYPE)msg.pop_byte();
		player_card_position = msg.pop_byte();
		byte same_count_with_player = msg.pop_byte();
		slot_index = msg.pop_byte();
		//Debug.Log("on select card ack. " + slot_index);

		card_event = (CARD_EVENT_TYPE)msg.pop_byte();
		//Debug.Log("-------------------- event " + card_event);
		switch (card_event)
		{
			case CARD_EVENT_TYPE.BOMB:
				{
					byte bomb_card_count = (byte)msg.pop_byte();
					for (byte i = 0; i < bomb_card_count; ++i)
					{
						byte number = msg.pop_byte();
						PAE_TYPE pae_type = (PAE_TYPE)msg.pop_byte();
						byte position = msg.pop_byte();
						CCard card = this.card_manager.find_card(number, pae_type, position);
						bomb_cards_info.Add(card);

						//UnityEngine.Debug.Log(string.Format("BOMB {0}, {1}, {2}", number, pae_type, position));
					}
				}
				break;

			case CARD_EVENT_TYPE.SHAKING:
				{
					byte shaking_card_count = (byte)msg.pop_byte();
					for (byte i = 0; i < shaking_card_count; ++i)
					{
						byte number = msg.pop_byte();
						PAE_TYPE pae_type = (PAE_TYPE)msg.pop_byte();
						byte position = msg.pop_byte();
						CCard card = this.card_manager.find_card(number, pae_type, position);
						shaking_cards_info.Add(card);

						//UnityEngine.Debug.Log(string.Format("SHAKING {0}, {1}, {2}", number, pae_type, position));
					}
				}
				break;
		}


		List<Sprite> target_to_choice = new List<Sprite>();
		PLAYER_SELECT_CARD_RESULT select_result = (PLAYER_SELECT_CARD_RESULT)msg.pop_byte();
		if (select_result == PLAYER_SELECT_CARD_RESULT.CHOICE_ONE_CARD_FROM_PLAYER)
		{
			byte count = msg.pop_byte();
			for (byte i = 0; i < count; ++i)
			{
				byte number = msg.pop_byte();
				PAE_TYPE pae_type = (PAE_TYPE)msg.pop_byte();
				byte position = msg.pop_byte();

				CCard card = this.card_manager.find_card(number, pae_type, position);
				target_to_choice.Add(get_hwatoo_sprite(card));
			}
		}
		// 파싱 끝 ------------------------------------------------


        yield return StartCoroutine(delay_if_exist(delay));

		hide_hint_mark();
		refresh_player_floor_slots(PAE_TYPE.PEE, player_index);

		// 화면 연출 진행.
		// 흔들었을 경우 흔든 카드의 정보를 출력해 준다.
		if (card_event == CARD_EVENT_TYPE.SHAKING)
		{
			CUIManager.Instance.show(UI_PAGE.POPUP_SHAKING_CARDS);
			CPopupShakingCards popup =
				CUIManager.Instance.get_uipage(UI_PAGE.POPUP_SHAKING_CARDS).GetComponent<CPopupShakingCards>();
			List<Sprite> sprites = new List<Sprite>();
			for (int i = 0; i < shaking_cards_info.Count; ++i)
			{
				sprites.Add(get_hwatoo_sprite(shaking_cards_info[i]));
			}
			popup.refresh(sprites);

			yield return new WaitForSeconds(1.5f);
			CUIManager.Instance.hide(UI_PAGE.POPUP_SHAKING_CARDS);
		}

		// 플레이어가 낸 카드 움직이기.
		yield return StartCoroutine(move_player_cards_to_floor(
			player_index,
			card_event,
			bomb_cards_info,
			slot_index, player_card_number, player_card_pae_type, player_card_position));

		yield return new WaitForSeconds(0.3f);


		if (card_event != CARD_EVENT_TYPE.NONE)
		{
			// 흔들기는 위에서 팝업으로 보여줬기 때문에 별도의 이펙트는 필요 없다.
			if (card_event != CARD_EVENT_TYPE.SHAKING)
			{
				CEffectManager.Instance.play(card_event);
				yield return new WaitForSeconds(1.5f);
			}
		}


		if (player_index == this.player_me_index)
		{
			// 바닥에 깔린 카드가 두장일 때 둘중 하나를 선택하는 팝업을 출력한다.
			if (select_result == PLAYER_SELECT_CARD_RESULT.CHOICE_ONE_CARD_FROM_PLAYER)
			{
				CUIManager.Instance.show(UI_PAGE.POPUP_CHOICE_CARD);
				CPopupChoiceCard popup =
					CUIManager.Instance.get_uipage(UI_PAGE.POPUP_CHOICE_CARD).GetComponent<CPopupChoiceCard>();
				popup.refresh(select_result, target_to_choice[0], target_to_choice[1]);
			}
			else
			{
				// 가운데 카드 뒤집기 요청.
				CPacket flip_msg = CPacket.create((short)PROTOCOL.FLIP_DECK_CARD_REQ);
				CNetworkManager.Instance.send(flip_msg);
			}
		}
	}


	IEnumerator move_flip_card(byte number, PAE_TYPE pae_type, byte position)
	{
		// 뒤집은 카드 움직이기.
		CCardPicture deck_card_picture = this.deck_cards.Pop();
		CCard flipped_card = this.card_manager.find_card(number, pae_type, position);
		deck_card_picture.update_card(flipped_card, get_hwatoo_sprite(flipped_card));
		yield return StartCoroutine(flip_deck_card(deck_card_picture));

		yield return new WaitForSeconds(0.3f);

		deck_card_picture.transform.localScale = SCALE_TO_FLOOR;
		move_card_to_floor(deck_card_picture, CARD_EVENT_TYPE.NONE);

		yield return new WaitForSeconds(0.5f);
	}


	IEnumerator on_flip_deck_card_ack(CPacket msg)
	{
		hide_hint_mark();

		byte player_index = msg.pop_byte();

		// 덱에서 뒤집은 카드 정보.
		byte deck_card_number = msg.pop_byte();
		PAE_TYPE deck_card_pae_type = (PAE_TYPE)msg.pop_byte();
		byte deck_card_position = msg.pop_byte();
		byte same_count_with_deck = msg.pop_byte();

		List<Sprite> target_to_choice = new List<Sprite>();
		PLAYER_SELECT_CARD_RESULT result = (PLAYER_SELECT_CARD_RESULT)msg.pop_byte();
		if (result == PLAYER_SELECT_CARD_RESULT.CHOICE_ONE_CARD_FROM_DECK)
		{
			byte count = msg.pop_byte();
			for (byte i = 0; i < count; ++i)
			{
				byte number = msg.pop_byte();
				PAE_TYPE pae_type = (PAE_TYPE)msg.pop_byte();
				byte position = msg.pop_byte();

				CCard card = this.card_manager.find_card(number, pae_type, position);
				target_to_choice.Add(get_hwatoo_sprite(card));
			}


			yield return StartCoroutine(move_flip_card(deck_card_number, deck_card_pae_type, deck_card_position));

			if (player_index == this.player_me_index)
			{
				CUIManager.Instance.show(UI_PAGE.POPUP_CHOICE_CARD);
				CPopupChoiceCard popup =
					CUIManager.Instance.get_uipage(UI_PAGE.POPUP_CHOICE_CARD).GetComponent<CPopupChoiceCard>();
				popup.refresh(result, target_to_choice[0], target_to_choice[1]);
			}
		}
		else
		{
			List<CCard> cards_to_give = parse_cards_to_get(msg);
			List<CCardPicture> take_cards_from_others = parse_cards_to_take_from_others(player_index, msg);
			List<CARD_EVENT_TYPE> events = parse_flip_card_events(msg);


			refresh_player_floor_slots(PAE_TYPE.PEE, player_index);

			// 화면 연출 진행.
			yield return StartCoroutine(move_flip_card(deck_card_number, deck_card_pae_type, deck_card_position));


			if (events.Count > 0)
			{
				for (int i = 0; i < events.Count; ++i)
				{
					CEffectManager.Instance.play(events[i]);
					yield return new WaitForSeconds(1.5f);
				}
			}


			yield return StartCoroutine(move_after_flip_card(player_index, take_cards_from_others, cards_to_give));
		}
	}


	List<CARD_EVENT_TYPE> parse_flip_card_events(CPacket msg)
	{
		List<CARD_EVENT_TYPE> events = new List<CARD_EVENT_TYPE>();
		byte count = msg.pop_byte();
		for (byte i = 0; i < count; ++i)
		{
			CARD_EVENT_TYPE type = (CARD_EVENT_TYPE)msg.pop_byte();
			events.Add(type);
		}

		return events;
	}


	IEnumerator move_after_flip_card(byte player_index,
		List<CCardPicture> take_cards_from_others,
		List<CCard> cards_to_give)
	{
		// 상대방에게 뺏어올 카드 움직이기.
		for (int i = 0; i < take_cards_from_others.Count; ++i)
		{
			Vector3 pos = get_player_card_position(player_index, PAE_TYPE.PEE);
			move_card(take_cards_from_others[i],
				take_cards_from_others[i].transform.position,
				pos);
			this.player_card_manager[player_index].add(take_cards_from_others[i]);

			yield return new WaitForSeconds(0.5f);
		}


		// 카드 가져오기.
		for (int i = 0; i < cards_to_give.Count; ++i)
		{
			CVisualFloorSlot slot =
				this.floor_ui_slots.Find(obj => obj.is_same_card(cards_to_give[i].number));
			if (slot == null)
			{
				UnityEngine.Debug.LogError(string.Format("Cannot find floor slot. {0}, {1}, {2}",
					cards_to_give[i].number, cards_to_give[i].pae_type, cards_to_give[i].position));
			}
			CCardPicture card_pic = slot.find_card(cards_to_give[i]);

			if (card_pic == null)
			{
				UnityEngine.Debug.LogError(string.Format("Cannot find the card. {0}, {1}, {2}",
					cards_to_give[i].number, cards_to_give[i].pae_type, cards_to_give[i].position));
			}

			slot.remove_card(card_pic);

			Vector3 begin = card_pic.transform.position;
			Vector3 to = get_player_card_position(player_index, card_pic.card.pae_type);

			if (this.player_me_index == player_index)
			{
				card_pic.transform.localScale = SCALE_TO_MY_FLOOR;
			}
			else
			{
				card_pic.transform.localScale = SCALE_TO_OTHER_FLOOR;
			}

			move_card(card_pic, begin, to);

			this.player_card_manager[player_index].add(card_pic);

			yield return new WaitForSeconds(0.1f);
		}


		//yield return new WaitForSeconds(0.5f);

		sort_floor_cards_when_finished_turn();
		refresh_player_hand_slots(player_index);

		yield return new WaitForSeconds(0.2f);

		CPacket finish = CPacket.create((short)PROTOCOL.TURN_END);
		CNetworkManager.Instance.send(finish);
	}


	IEnumerator flip_deck_card(CCardPicture deck_card_picture)
	{
		Animator ani = deck_card_picture.GetComponentInChildren<Animator>();
		ani.enabled = true;
		ani.Play("rotation");

		yield return StartCoroutine(scale_to(
			deck_card_picture,
			3.0f,
			0.1f));
	}


	/// <summary>
	/// 플레이어가 선택한 카드를 바닥에 내는 장면 구현.
	/// 폭탄 이벤트가 존재할 경우 같은 번호의 카드 세장을 한꺼번에 내도록 구현한다.
	/// </summary>
	/// <param name="player_index"></param>
	/// <param name="event_type"></param>
	/// <param name="slot_index"></param>
	/// <param name="player_card_number"></param>
	/// <param name="player_card_pae_type"></param>
	/// <param name="player_card_position"></param>
	/// <returns></returns>
	IEnumerator move_player_cards_to_floor(
		byte player_index,
		CARD_EVENT_TYPE event_type,
		List<CCard> bomb_cards_info,
		byte slot_index,
		byte player_card_number,
		PAE_TYPE player_card_pae_type,
		byte player_card_position)
	{
		float card_moving_delay = 0.2f;

		List<CCardPicture> targets = new List<CCardPicture>();
		if (event_type == CARD_EVENT_TYPE.BOMB)
		{
			card_moving_delay = 0.1f;

			// 폭탄인 경우에는 폭탄 카드 수 만큼 낸다.
			if (this.player_me_index == player_index)
			{
				for (int i = 0; i < bomb_cards_info.Count; ++i)
				{
					CCardPicture card_picture = this.player_hand_card_manager[player_index].find_card(
						bomb_cards_info[i].number, bomb_cards_info[i].pae_type, bomb_cards_info[i].position);
					targets.Add(card_picture);
				}
			}
			else
			{
				for (int i = 0; i < bomb_cards_info.Count; ++i)
				{
					CCardPicture card_picture = this.player_hand_card_manager[player_index].get_card(i);
					CCard card = this.card_manager.find_card(bomb_cards_info[i].number,
						bomb_cards_info[i].pae_type, bomb_cards_info[i].position);
					card_picture.update_card(card, get_hwatoo_sprite(card));
					targets.Add(card_picture);
				}
			}
		}
		else
		{
			// 폭탄이 아닌 경우에는 한장의 카드만 낸다.
			CCardPicture card_picture = this.player_hand_card_manager[player_index].get_card(slot_index);
			targets.Add(card_picture);

			if (this.player_me_index != player_index)
			{
				CCard card = this.card_manager.find_card(player_card_number,
					player_card_pae_type, player_card_position);
				card_picture.update_card(card, get_hwatoo_sprite(card));
			}
		}

		if (event_type == CARD_EVENT_TYPE.BOMB)
		{
			CVisualFloorSlot slot =
				this.floor_ui_slots.Find(obj => obj.is_same_card(player_card_number));
			Vector3 to = get_ui_slot_position(slot);
			CEffectManager.Instance.play_dust(to, 0.3f, true);
		}


		// 카드 움직이기.
		for (int i = 0; i < targets.Count; ++i)
		{
			// 손에 들고 있는 패에서 제거한다.
			CCardPicture player_card = targets[i];
			this.player_hand_card_manager[player_index].remove(player_card);

			// 스케일 장면.
			yield return StartCoroutine(scale_and_move_to_center(
				player_card,
				5.0f,
				0.05f));

			yield return new WaitForSeconds(card_moving_delay);
			
			Input.gyro.enabled = true;

			Vector3 previousGyro = Vector3.zero; // 이전 프레임의 자이로 값
			bool isShaken = false;

			if (player_index == this.player_me_index)
			{
				CameraShake cameraShake = Camera.main.GetComponent<CameraShake>();
				// 카드가 내려놓아지는 조건
				while (true && !isShaken)
				{
					 Vector3 gyroRotation = Input.gyro.rotationRateUnbiased;
        			 Vector3 gyroDelta = gyroRotation - previousGyro; // 가속도 변화량 계산

					// 📌 강하게 흔들었을 때만 반응 (기울기 변화 8.0 이상 + 순간 가속도 변화 8.0 이상)
        			if (((Mathf.Abs(gyroRotation.x) >= 2.0f && Mathf.Abs(gyroRotation.x) < 3.0f) 
						|| (Mathf.Abs(gyroRotation.y) >= 2.0f && Mathf.Abs(gyroRotation.y) < 3.0f)) 
						&& ((Mathf.Abs(gyroDelta.x) >= 1.0f && Mathf.Abs(gyroDelta.x) < 3.0f)
						|| (Mathf.Abs(gyroDelta.y) >= 1.0f && Mathf.Abs(gyroDelta.y) < 3.0f)))
					{
						Debug.Log("Card drop detected based on gyro input.");
						// 0.5초 후에 1초 동안 진동 실행
						// StartCoroutine(VibrateWithDelay(1000, 0.5f));
						// 내 패를 제출 할때만 진동

						Handheld.Vibrate();
						imageController.ShowImage(1);
						isShaken = true;
						// 📌 카드 제출 후 다시 강한 흔들림이 필요하도록 초기화
            			yield return new WaitForSeconds(0.5f); // 대기 후 다시 감지
            			previousGyro = Vector3.zero; // 이전 값 초기화 (다시 강한 흔들림 필요)
						//break;
					}else if (((Mathf.Abs(gyroRotation.x) >= 3.0f && Mathf.Abs(gyroRotation.x) < 4.0f) 
						|| (Mathf.Abs(gyroRotation.y) >= 3.0f && Mathf.Abs(gyroRotation.y) < 4.0f)) 
						&& ((Mathf.Abs(gyroDelta.x) >= 3.0f && Mathf.Abs(gyroDelta.x) < 5.0f)
						|| (Mathf.Abs(gyroDelta.y) >= 3.0f && Mathf.Abs(gyroDelta.y) < 5.0f))){
						
						Handheld.Vibrate();
						imageController.ShowImage(2);
						StartCoroutine(cameraShake.Shake(0.5f, 3f)); //카메라 흔들림(시간,세기)
						isShaken = true;
						// 📌 카드 제출 후 다시 강한 흔들림이 필요하도록 초기화
            			yield return new WaitForSeconds(0.5f); // 대기 후 다시 감지
            			previousGyro = Vector3.zero; // 이전 값 초기화 (다시 강한 흔들림 필요)

					}else if ((Mathf.Abs(gyroRotation.x) >= 4.0f
						|| Mathf.Abs(gyroRotation.y) >= 4.0f) 
						&& (Mathf.Abs(gyroDelta.x) >= 5.0f
						|| Mathf.Abs(gyroDelta.y) >= 5.0f))
						{

						Handheld.Vibrate();
						imageController.ShowImage(3);
						StartCoroutine(cameraShake.Shake(0.7f, 5f)); //카메라 흔들림(시간,세기)
						isShaken = true;
						// 📌 카드 제출 후 다시 강한 흔들림이 필요하도록 초기화
            			yield return new WaitForSeconds(0.5f); // 대기 후 다시 감지
            			previousGyro = Vector3.zero; // 이전 값 초기화 (다시 강한 흔들림 필요)

					}
					yield return null;
				}
			}

			// 이동 장면.
			player_card.transform.localScale = SCALE_TO_FLOOR;
			move_card_to_floor(player_card, event_type);
		}
	}

	// 휴대폰 진동 추가
	IEnumerator VibrateWithDelay(long duration, float delay)
	{
		yield return new WaitForSeconds(delay); // 지정한 시간만큼 대기
		VibrationManager.Vibrate(duration); // 지정한 시간(duration) 동안 진동 실행
	}
	
	// 카드 스케일 확대 후 화면 가운데 배치
	IEnumerator scale_and_move_to_center(CCardPicture card_picture, float ratio, float duration)
	{
    	card_picture.sprite_renderer.sortingOrder = CSpriteLayerOrderManager.Instance.Order;

    	Vector3 fromPos = card_picture.transform.position; // 원래 위치
    	Vector3 fromScale = card_picture.transform.localScale; // 원래 크기

    	// 📌 UI Canvas의 중심 좌표 구하기
    	RectTransform canvasRect = FindObjectOfType<Canvas>().GetComponent<RectTransform>();
    	Vector3 screenCenter = new Vector3(Screen.width / 2, Screen.height / 2, 0);
    
    	// 📌 World 좌표로 변환 (카드가 UI 위에서 정렬되는 경우)
    	Vector3 toPos;
    	RectTransformUtility.ScreenPointToWorldPointInRectangle(canvasRect, screenCenter, Camera.main, out toPos);

    	Vector3 toScale = fromScale * ratio;

    	float begin = Time.time;
    	while (Time.time - begin <= duration)
    	{
        	float t = (Time.time - begin) / duration;

        	// 위치 변경 (World Position)
        	card_picture.transform.position = Vector3.Lerp(fromPos, toPos, t);

        	// 스케일 변경
        	card_picture.transform.localScale = Vector3.Lerp(fromScale, toScale, t);

        	yield return null;
    	}

    	// 최종 위치 & 스케일 설정
    	card_picture.transform.position = toPos;
    	card_picture.transform.localScale = toScale;
	}

	
	// 카드 스케일 확대 
	IEnumerator scale_to(CCardPicture card_picture, float ratio, float duration)
	{
		card_picture.sprite_renderer.sortingOrder = CSpriteLayerOrderManager.Instance.Order;

		Vector3 from = card_picture.transform.localScale;
		float begin = Time.time;
		Vector3 to = from * ratio;
		while (Time.time - begin <= duration)
		{
			float t = (Time.time - begin) / duration;

			Vector3 scale = from;
			scale.x = EasingUtil.linear(from.x, to.x, t);
			scale.y = EasingUtil.linear(from.y, to.y, t);

			card_picture.transform.localScale = scale;

			yield return 0;
		}

		card_picture.transform.localScale = to;
	}


	void move_card_to_floor(CCardPicture card_picture, CARD_EVENT_TYPE event_type)
	{
		byte slot_index = 0;
		Vector3 begin = card_picture.transform.position;
		Vector3 to = Vector3.zero;

		CVisualFloorSlot slot =
			this.floor_ui_slots.Find(obj => obj.is_same_card(card_picture.card.number));
		if (slot == null)
		{
			byte empty_slot = find_empty_floorslot();
			//Debug.Log(string.Format("empty slot pos " + empty_slot));
			to = this.floor_slot_position[empty_slot];
			slot_index = empty_slot;
		}
		else
		{
			to = get_ui_slot_position(slot);

			List<CCardPicture> floor_card_pictures = slot.get_cards();
			for (int i = 0; i < floor_card_pictures.Count; ++i)
			{
				Animator ani = floor_card_pictures[i].GetComponentInChildren<Animator>();
				ani.enabled = true;
				ani.Play("card_hit_under");
			}

			slot_index = slot.ui_slot_position;

			if (event_type != CARD_EVENT_TYPE.BOMB)
			{
				CEffectManager.Instance.play_dust(to, 0.1f, false);
			}

			Animator card_ani = card_picture.GetComponentInChildren<Animator>();
			card_ani.enabled = true;
			card_ani.Play("card_hit");
		}

		// 바닥 카드로 등록.
		this.floor_ui_slots[slot_index].add_card(card_picture);
		move_card(card_picture, begin, to, 0.01f);
	}


	byte find_empty_floorslot()
	{
		CVisualFloorSlot slot = this.floor_ui_slots.Find(obj => obj.get_card_count() == 0);
		if (slot == null)
		{
			return byte.MaxValue;
		}

		return slot.ui_slot_position;
	}


	/// <summary>
	/// 플레이어의 패를 번호 순서에 따라 오름차순 정렬 한다.
	/// </summary>
	/// <param name="player_index"></param>
	void sort_player_hand_slots(byte player_index)
	{
		this.player_hand_card_manager[player_index].sort_by_number();
		refresh_player_hand_slots(player_index);
	}


	/// <summary>
	/// 플레이어의 패의 위치를 갱신한다.
	/// 패를 내면 중간중간 빠진 자리가 생기는데 그 자리를 처음부터 다시 채워준다.
	/// </summary>
	/// <param name="player_index"></param>
	void refresh_player_hand_slots(byte player_index)
	{
		CPlayerHandCardManager hand_card_manager = this.player_hand_card_manager[player_index];
		byte count = (byte)hand_card_manager.get_card_count();
		for (byte card_index = 0; card_index < count; ++card_index)
		{
			CCardPicture card = hand_card_manager.get_card(card_index);
			// 슬롯 인덱스를 재설정 한다.
			card.set_slot_index(card_index);

			// 화면 위치를 재설정 한다.
			card.transform.position = this.player_card_positions[player_index].get_hand_position(card_index);
		}
	}


	/// <summary>
	/// 플레이어의 바닥 카드 위치를 갱신한다.
	/// 피를 뺏기거나 옮기거나 했을 때 생기는 빈자리를 채워준다.
	/// </summary>
	/// <param name="player_index"></param>
	void refresh_player_floor_slots(PAE_TYPE pae_type, byte player_index)
	{
		int count = this.player_card_manager[player_index].get_card_count(pae_type);
		for (int i = 0; i < count; ++i)
		{
			Vector3 pos = this.player_card_positions[player_index].get_floor_position(i, pae_type);
			CCardPicture card_pic = this.player_card_manager[player_index].get_card_at(pae_type, i);
			pos.z = card_pic.transform.position.z;
			card_pic.transform.position = pos;
		}
	}


	Vector3 get_player_card_position(byte player_index, PAE_TYPE pae_type)
	{
		int count = this.player_card_manager[player_index].get_card_count(pae_type);
		return this.player_card_positions[player_index].get_floor_position(count, pae_type);
	}


	void on_card_touch(CCardPicture card_picture)
	{
		// 카드 연속 터치등을 막기 위한 처리.
		this.card_collision_manager.enabled = false;
		this.ef_focus.SetActive(false);

		int count = this.player_hand_card_manager.Count;
		for (int i = 0; i < count; ++i)
		{
			this.player_hand_card_manager[i].enable_all_colliders(false);
		}


		// 일반 카드, 폭탄 카드에 따라 다르게 처리한다.
		if (card_picture.is_back_card())
		{
			CPacket msg = CPacket.create((short)PROTOCOL.FLIP_BOMB_CARD_REQ);
			CNetworkManager.Instance.send(msg);
		}
		else
		{
			// 손에 같은 카드 3장이 있고 바닥에 같은카드가 없을 때 흔들기 팝업을 출력한다.
			int same_on_hand = 
				this.player_hand_card_manager[this.player_me_index].get_same_number_count(card_picture.card.number);
			int same_on_floor = get_same_number_count_on_floor(card_picture.card.number);
			if (same_on_hand == 3 && same_on_floor == 0)
			{
				CUIManager.Instance.show(UI_PAGE.POPUP_ASK_SHAKING);
				CPopupShaking popup =
					CUIManager.Instance.get_uipage(UI_PAGE.POPUP_ASK_SHAKING).GetComponent<CPopupShaking>();
				popup.refresh(card_picture.card, card_picture.slot);
			}
			else
			{
				CPlayRoomUI.send_select_card(card_picture.card, card_picture.slot, 0);
			}
		}
	}


	int get_same_number_count_on_floor(byte number)
	{
		List<CVisualFloorSlot> slots = 
			this.floor_ui_slots.FindAll(obj => obj.is_same_card(number));
		return slots.Count;
	}


	//------------------------------------------------------------------------------
	// UI효과 관련 매소드. 다른 클래스로 빠질 가능성이 있는 부분이다.
	// 힌트 화살표.
	CGameObjectPool<GameObject> hint_arrows;
	List<GameObject> enabled_hint_arrows;
	void load_hint_arrows()
	{
		this.enabled_hint_arrows = new List<GameObject>();
		GameObject arrow = Resources.Load("hint") as GameObject;
		this.hint_arrows = new CGameObjectPool<GameObject>(10, arrow, (GameObject original) =>
		{
			GameObject clone = GameObject.Instantiate(original) as GameObject;
			clone.SetActive(false);
			return clone;
		});
	}


	void hide_hint_mark()
	{
		for (int i = 0; i < this.enabled_hint_arrows.Count; ++i)
		{
			this.enabled_hint_arrows[i].SetActive(false);
			this.hint_arrows.push(this.enabled_hint_arrows[i]);
		}

		this.enabled_hint_arrows.Clear();
	}


	public void refresh_hint_mark()
	{
		hide_hint_mark();

		for (int i = 0; i < this.player_hand_card_manager[this.player_me_index].get_card_count(); ++i)
		{
			CCardPicture card_picture = this.player_hand_card_manager[this.player_me_index].get_card(i);
			CVisualFloorSlot slot =
				this.floor_ui_slots.Find(obj => obj.is_same_card(card_picture.card.number));
			if (slot == null)
			{
				continue;
			}

			show_hint_mark(card_picture.transform.position);
		}
	}


	void show_hint_mark(Vector3 position)
	{
		bool option_hint = 
			CUIManager.Instance.get_uipage(UI_PAGE.GAME_OPTION).GetComponent<CGameOption>().is_hint_on();

		if (!option_hint)
		{
			return;
		}

		GameObject hint = this.hint_arrows.pop();
		hint.SetActive(true);
		hint.transform.position = position;

		this.enabled_hint_arrows.Add(hint);
	}


	bool is_me(byte player_index)
	{
		return this.player_me_index == player_index;
	}
	//------------------------------------------------------------------------------



	//------------------------------------------------------------------------------
	// static 매소드.
	public static void send_select_card(CCard card, byte slot, byte is_shaking)
	{
		CPacket msg = CPacket.create((short)PROTOCOL.SELECT_CARD_REQ);
		msg.push(card.number);
		msg.push((byte)card.pae_type);
		msg.push(card.position);
		msg.push(slot);
		msg.push(is_shaking);
		CNetworkManager.Instance.send(msg);
	}
	//------------------------------------------------------------------------------
}
